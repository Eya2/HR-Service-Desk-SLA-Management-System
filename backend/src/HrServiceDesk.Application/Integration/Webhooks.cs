using System.Text.Json;
using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Audit;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Audit;
using HrServiceDesk.Domain.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Integration;

public sealed record WebhookDto(
    Guid Id,
    string Name,
    string Url,
    IReadOnlyList<string> Events,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int PendingDeliveries,
    int FailedDeliveries,
    DateTimeOffset? LastDeliveredAt);

/// <summary>The saved subscription; <see cref="Secret"/> is set only when it was created or the secret rotated.</summary>
public sealed record WebhookSavedDto(WebhookDto Webhook, string? Secret);

public sealed record WebhookDeliveryDto(
    Guid Id,
    string EventType,
    Guid? TicketId,
    string Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextAttemptAt,
    int? LastStatusCode,
    string? LastError,
    string Payload);

public sealed record ListWebhooksQuery : IRequest<IReadOnlyList<WebhookDto>>;

public sealed record SaveWebhookCommand(Guid? Id, string Name, string Url, IReadOnlyList<string> Events, bool IsActive, bool RotateSecret)
    : IRequest<Result<WebhookSavedDto>>;

public sealed record DeleteWebhookCommand(Guid Id) : IRequest<Result>;

public sealed record ListWebhookDeliveriesQuery(Guid SubscriptionId, int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<WebhookDeliveryDto>>>;

public sealed record RedeliverWebhookCommand(Guid DeliveryId) : IRequest<Result>;

/// <summary>Queues a "ping" event to check that the receiver answers and verifies signatures.</summary>
public sealed record PingWebhookCommand(Guid SubscriptionId) : IRequest<Result>;

internal sealed class SaveWebhookValidator : AbstractValidator<SaveWebhookCommand>
{
    public SaveWebhookValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(WebhookSubscription.NameMaxLength);
        RuleFor(c => c.Url).NotEmpty().MaximumLength(WebhookSubscription.UrlMaxLength);
        RuleFor(c => c.Events).NotEmpty();
        RuleForEach(c => c.Events).Must(e => WebhookEvents.All.Contains(e)).WithMessage($"Events must be among: {string.Join(", ", WebhookEvents.All)}.");
    }
}

internal sealed class WebhookHandlers(
    IAppDbContext db,
    IWebhookSecretProtector protector,
    IIntegrationSettings settings,
    IWebhookDispatchTrigger trigger,
    AuditTrail audit,
    TimeProvider clock)
    : IRequestHandler<ListWebhooksQuery, IReadOnlyList<WebhookDto>>,
      IRequestHandler<SaveWebhookCommand, Result<WebhookSavedDto>>,
      IRequestHandler<DeleteWebhookCommand, Result>,
      IRequestHandler<ListWebhookDeliveriesQuery, Result<PagedResult<WebhookDeliveryDto>>>,
      IRequestHandler<RedeliverWebhookCommand, Result>,
      IRequestHandler<PingWebhookCommand, Result>
{
    private static readonly Error NotFound = Error.NotFound("webhook.not_found", "Webhook not found.");
    private static readonly Error DeliveryNotFound = Error.NotFound("webhook.delivery_not_found", "Delivery not found.");

    public async Task<IReadOnlyList<WebhookDto>> Handle(ListWebhooksQuery request, CancellationToken cancellationToken)
    {
        var subscriptions = await db.WebhookSubscriptions.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
        var stats = await db.WebhookDeliveries.AsNoTracking()
            .GroupBy(d => d.SubscriptionId)
            .Select(g => new
            {
                g.Key,
                Pending = g.Count(d => d.Status == WebhookDeliveryStatus.Pending),
                Failed = g.Count(d => d.Status == WebhookDeliveryStatus.Failed),
                Last = g.Max(d => d.DeliveredAt),
            })
            .ToDictionaryAsync(x => x.Key, cancellationToken);
        return subscriptions.Select(s => stats.TryGetValue(s.Id, out var st) ? ToDto(s, st.Pending, st.Failed, st.Last) : ToDto(s, 0, 0, null)).ToList();
    }

    public async Task<Result<WebhookSavedDto>> Handle(SaveWebhookCommand request, CancellationToken cancellationToken)
    {
        string? secret = null;
        WebhookSubscription subscription;
        if (request.Id is { } id)
        {
            var existing = await db.WebhookSubscriptions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (existing is null)
                return NotFound;
            subscription = existing;
            subscription.Update(request.Name, request.Url, request.Events, request.IsActive, settings.AllowInsecureWebhookUrls);
            if (request.RotateSecret)
            {
                secret = WebhookSignature.NewSecret();
                subscription.RotateSecret(protector.Protect(secret));
            }
        }
        else
        {
            secret = WebhookSignature.NewSecret();
            subscription = WebhookSubscription.Create(request.Name, request.Url, request.Events, protector.Protect(secret), settings.AllowInsecureWebhookUrls);
            if (!request.IsActive)
                subscription.Update(request.Name, request.Url, request.Events, isActive: false, settings.AllowInsecureWebhookUrls);
            db.WebhookSubscriptions.Add(subscription);
        }

        audit.Add(AuditAction.WebhookChanged, nameof(WebhookSubscription), subscription.Id,
            $"Webhook \"{subscription.Name}\" saved ({subscription.Url}; {string.Join(", ", subscription.Events)}{(secret is null ? string.Empty : "; new secret")})");
        await db.SaveChangesAsync(cancellationToken);
        return new WebhookSavedDto(ToDto(subscription, 0, 0, null), secret);
    }

    public async Task<Result> Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
    {
        var subscription = await db.WebhookSubscriptions.SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (subscription is null)
            return NotFound;
        db.WebhookSubscriptions.Remove(subscription);
        audit.Add(AuditAction.WebhookChanged, nameof(WebhookSubscription), subscription.Id, $"Webhook \"{subscription.Name}\" deleted");
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PagedResult<WebhookDeliveryDto>>> Handle(ListWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        if (!await db.WebhookSubscriptions.AnyAsync(s => s.Id == request.SubscriptionId, cancellationToken))
            return NotFound;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);
        var query = db.WebhookDeliveries.AsNoTracking().Where(d => d.SubscriptionId == request.SubscriptionId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<WebhookDeliveryDto>(
            items.Select(d => new WebhookDeliveryDto(
                d.Id, d.EventType, d.TicketId, d.Status.ToString(), d.Attempts, d.CreatedAt, d.LastAttemptAt,
                d.Status == WebhookDeliveryStatus.Pending ? d.NextAttemptAt : null, d.LastStatusCode, d.LastError, d.Payload)).ToList(),
            page, pageSize, total);
    }

    public async Task<Result> Handle(RedeliverWebhookCommand request, CancellationToken cancellationToken)
    {
        var delivery = await db.WebhookDeliveries.SingleOrDefaultAsync(d => d.Id == request.DeliveryId, cancellationToken);
        if (delivery is null)
            return DeliveryNotFound;
        delivery.Redeliver(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        trigger.Kick();
        return Result.Success();
    }

    public async Task<Result> Handle(PingWebhookCommand request, CancellationToken cancellationToken)
    {
        var subscription = await db.WebhookSubscriptions.SingleOrDefaultAsync(s => s.Id == request.SubscriptionId, cancellationToken);
        if (subscription is null)
            return NotFound;
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow();
        var payload = JsonSerializer.Serialize(new { id, type = WebhookEvents.Ping, occurredAt = now, data = new { subscriptionId = subscription.Id } }, WebhookPlanner.Json);
        db.WebhookDeliveries.Add(WebhookDelivery.Create(id, subscription, WebhookEvents.Ping, payload, null, now));
        await db.SaveChangesAsync(cancellationToken);
        trigger.Kick();
        return Result.Success();
    }

    private static WebhookDto ToDto(WebhookSubscription s, int pending, int failed, DateTimeOffset? last) =>
        new(s.Id, s.Name, s.Url, s.Events, s.IsActive, s.CreatedAt, pending, failed, last);
}
