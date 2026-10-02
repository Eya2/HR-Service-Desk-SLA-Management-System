using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HrServiceDesk.Application.Integration;

/// <summary>Sends the deliveries that are due (all organisations), oldest first. Returns how many were attempted.</summary>
public sealed record DispatchWebhooksCommand(int BatchSize = 50) : IRequest<int>;

internal sealed partial class DispatchWebhooksHandler(
    IAppDbContext db,
    IWebhookSender sender,
    IWebhookSecretProtector protector,
    TimeProvider clock,
    ILogger<DispatchWebhooksHandler> logger) : IRequestHandler<DispatchWebhooksCommand, int>
{
    public async Task<int> Handle(DispatchWebhooksCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.WebhookDeliveries.IgnoreQueryFilters()
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt)
            .Take(request.BatchSize)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
            return 0;

        var subscriptionIds = due.Select(d => d.SubscriptionId).Distinct().ToList();
        var subscriptions = await db.WebhookSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var delivery in due)
        {
            if (!subscriptions.TryGetValue(delivery.SubscriptionId, out var subscription) || !subscription.IsActive)
            {
                delivery.RecordFailure(null, "The subscription is disabled.", clock.GetUtcNow());
            }
            else
            {
                var response = await sender.SendAsync(
                    new WebhookRequest(subscription.Url, delivery.Id, delivery.EventType, delivery.Payload, protector.Unprotect(subscription.ProtectedSecret)),
                    cancellationToken);
                if (response.Success)
                    delivery.RecordSuccess(response.StatusCode ?? 200, clock.GetUtcNow());
                else
                {
                    delivery.RecordFailure(response.StatusCode, response.Error ?? "Request failed.", clock.GetUtcNow());
                    LogFailed(logger, delivery.Id, subscription.Url, delivery.Attempts, response.Error);
                }
            }

            // Saved one by one: a crash mid-batch must not send the finished ones again.
            await db.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook delivery {DeliveryId} to {Url} failed (attempt {Attempt}): {Error}")]
    private static partial void LogFailed(ILogger logger, Guid deliveryId, string url, int attempt, string? error);
}
