using System.Text.Json;
using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Audit;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Domain.Audit;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Integration;

public sealed record IntegrationPersonDto(Guid Id, string FullName, string Email);

public sealed record IntegrationRequestTypeDto(Guid Id, string Name, string Category);

public sealed record IntegrationTicketSummaryDto(
    Guid Id,
    string Reference,
    string Title,
    string Status,
    string Priority,
    IntegrationRequestTypeDto RequestType,
    Guid RequesterId,
    Guid? TeamId,
    Guid? AssigneeId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record IntegrationCommentDto(Guid Id, string AuthorName, string Body, DateTimeOffset CreatedAt);

public sealed record IntegrationTicketDto(
    Guid Id,
    string Reference,
    string Title,
    string Description,
    string Status,
    string Priority,
    IntegrationRequestTypeDto RequestType,
    IntegrationPersonDto Requester,
    IntegrationPersonDto? Assignee,
    Guid? TeamId,
    JsonElement FormData,
    IReadOnlyList<IntegrationCommentDto> Comments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Cases of the key's organisation, most recently changed first. Confidential cases are never exposed.</summary>
public sealed record ListIntegrationTicketsQuery(DateTimeOffset? UpdatedSince, string? Status, string? Category, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<IntegrationTicketSummaryDto>>;

public sealed record GetIntegrationTicketQuery(Guid Id) : IRequest<Result<IntegrationTicketDto>>;

/// <summary>A public reply written by the integration's service account.</summary>
public sealed record AddIntegrationCommentCommand(Guid TicketId, string Body) : IRequest<Result<IntegrationCommentDto>>;

/// <summary>A status change with the rights of an HR agent (e.g. payroll marks a correction as resolved).</summary>
public sealed record ChangeIntegrationStatusCommand(Guid TicketId, string Status, string? Reason) : IRequest<Result>;

internal sealed class AddIntegrationCommentValidator : AbstractValidator<AddIntegrationCommentCommand>
{
    public AddIntegrationCommentValidator() => RuleFor(c => c.Body).NotEmpty().MaximumLength(Comment.BodyMaxLength);
}

internal sealed class ChangeIntegrationStatusValidator : AbstractValidator<ChangeIntegrationStatusCommand>
{
    public ChangeIntegrationStatusValidator()
    {
        RuleFor(c => c.Status).Must(s => Enum.TryParse<TicketStatus>(s, ignoreCase: false, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<TicketStatus>())}.");
        RuleFor(c => c.Reason).MaximumLength(Comment.BodyMaxLength);
        RuleFor(c => c.Reason).NotEmpty().When(c => c.Status == nameof(TicketStatus.Rejected)).WithMessage("A reason is required to reject a case.");
    }
}

internal sealed class IntegrationApiHandlers(IAppDbContext db, ICurrentUser currentUser, Sla.SlaService sla, AuditTrail audit, TimeProvider clock)
    : IRequestHandler<ListIntegrationTicketsQuery, PagedResult<IntegrationTicketSummaryDto>>,
      IRequestHandler<GetIntegrationTicketQuery, Result<IntegrationTicketDto>>,
      IRequestHandler<AddIntegrationCommentCommand, Result<IntegrationCommentDto>>,
      IRequestHandler<ChangeIntegrationStatusCommand, Result>
{
    /// <summary>The organisation's cases minus confidential ones (the tenant filter applies through the key's claims).</summary>
    private IQueryable<Ticket> Visible => db.Tickets.Where(t => !t.IsConfidential);

    public async Task<PagedResult<IntegrationTicketSummaryDto>> Handle(ListIntegrationTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = Visible.AsNoTracking();
        if (request.UpdatedSince is { } since)
        {
            var utc = since.ToUniversalTime();
            query = query.Where(t => (t.UpdatedAt ?? t.CreatedAt) >= utc);
        }

        if (Enum.TryParse<TicketStatus>(request.Status, out var status))
            query = query.Where(t => t.Status == status);
        if (Enum.TryParse<Domain.Catalog.RequestCategory>(request.Category, out var category))
            query = query.Where(t => db.RequestTypes.Any(rt => rt.Id == t.RequestTypeId && rt.Category == category));

        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var page = Math.Max(1, request.Page);
        var total = await query.CountAsync(cancellationToken);
        var tickets = await query.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var types = await RequestTypesAsync(tickets.Select(t => t.RequestTypeId), cancellationToken);
        return new PagedResult<IntegrationTicketSummaryDto>(
            tickets.Select(t => new IntegrationTicketSummaryDto(
                t.Id, t.Reference, t.Title, t.Status.ToString(), t.Priority.ToString(), types[t.RequestTypeId],
                t.RequesterId, t.TeamId, t.AssigneeId, t.CreatedAt, t.UpdatedAt ?? t.CreatedAt)).ToList(),
            page, pageSize, total);
    }

    public async Task<Result<IntegrationTicketDto>> Handle(GetIntegrationTicketQuery request, CancellationToken cancellationToken)
    {
        var ticket = await Visible.AsNoTracking().Include(t => t.Comments).SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;

        var types = await RequestTypesAsync([ticket.RequestTypeId], cancellationToken);
        var comments = ticket.Comments.Where(c => !c.IsInternal).OrderBy(c => c.CreatedAt).ToList();
        var userIds = comments.Select(c => c.AuthorId).Append(ticket.RequesterId).Concat(ticket.AssigneeId is { } a ? [a] : []).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new IntegrationPersonDto(u.Id, u.FirstName + " " + u.LastName, u.Email), cancellationToken);

        // Sensitive cases (bank details, leave) are read by a system: the access is recorded like a person's.
        if (ticket.IsSensitive)
            await audit.WriteAsync(AuditAction.SensitiveCaseViewed, nameof(Ticket), ticket.Id, $"{ticket.Reference} read through the integration API", cancellationToken);

        using var form = JsonDocument.Parse(ticket.FormData);
        return new IntegrationTicketDto(
            ticket.Id, ticket.Reference, ticket.Title, ticket.Description, ticket.Status.ToString(), ticket.Priority.ToString(),
            types[ticket.RequestTypeId], people[ticket.RequesterId],
            ticket.AssigneeId is { } assignee && people.TryGetValue(assignee, out var person) ? person : null,
            ticket.TeamId, form.RootElement.Clone(),
            comments.Select(c => new IntegrationCommentDto(c.Id, people.TryGetValue(c.AuthorId, out var p) ? p.FullName : string.Empty, c.Body, c.CreatedAt)).ToList(),
            ticket.CreatedAt, ticket.UpdatedAt ?? ticket.CreatedAt);
    }

    public async Task<Result<IntegrationCommentDto>> Handle(AddIntegrationCommentCommand request, CancellationToken cancellationToken)
    {
        var ticket = await Visible.SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;
        if (ticket.IsFinal)
            return TicketErrors.NotEditable;

        var now = clock.GetUtcNow();
        var comment = ticket.AddComment(currentUser.UserId!.Value, request.Body, isInternal: false, now);
        ticket.RecordFirstResponse(now);
        await sla.RefreshAsync(ticket, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var author = await db.Users.Where(u => u.Id == comment.AuthorId).Select(u => u.FirstName + " " + u.LastName).SingleAsync(cancellationToken);
        return new IntegrationCommentDto(comment.Id, author, comment.Body, comment.CreatedAt);
    }

    public async Task<Result> Handle(ChangeIntegrationStatusCommand request, CancellationToken cancellationToken)
    {
        var ticket = await Visible.Include(t => t.Approvals).SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;

        var to = Enum.Parse<TicketStatus>(request.Status);
        switch (TicketStatusMachine.Check(ticket.Status, to, TransitionActor.Agent))
        {
            case TransitionCheck.Invalid:
                return TicketErrors.InvalidTransition(ticket.Status, to);
            case TransitionCheck.NotPermitted:
                return TicketErrors.TransitionNotPermitted(ticket.Status, to);
        }

        var actorId = currentUser.UserId!.Value;
        var now = clock.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            ticket.AddComment(actorId, request.Reason, isInternal: false, now);
            ticket.RecordFirstResponse(now);
        }

        ticket.ChangeStatus(to, TransitionActor.Agent, actorId, now, request.Reason);
        await sla.RefreshAsync(ticket, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Dictionary<Guid, IntegrationRequestTypeDto>> RequestTypesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await db.RequestTypes.AsNoTracking().Where(t => list.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => new IntegrationRequestTypeDto(t.Id, t.Name, t.Category.ToString()), cancellationToken);
    }
}
