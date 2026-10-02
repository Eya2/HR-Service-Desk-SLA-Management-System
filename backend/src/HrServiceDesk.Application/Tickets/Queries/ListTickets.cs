using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Queries;

/// <summary>Which slice of the cases to list.</summary>
public enum TicketScope
{
    /// <summary>Every case the caller may see.</summary>
    All,

    /// <summary>Cases assigned to the caller.</summary>
    Mine,

    /// <summary>Cases of the teams the caller belongs to.</summary>
    MyTeams,

    /// <summary>Cases nobody is working on yet.</summary>
    Unassigned,
}

/// <summary>All cases the caller may see (HR staff and auditors), with filters.</summary>
public sealed record ListTicketsQuery(
    string? Status, string? Priority, Guid? RequestTypeId, string? Search, int Page = 1, int PageSize = 20,
    TicketScope Scope = TicketScope.All, Guid? TeamId = null, bool ActiveOnly = false)
    : IRequest<PagedResult<TicketSummaryDto>>;

internal sealed class ListTicketsValidator : AbstractValidator<ListTicketsQuery>
{
    public ListTicketsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Status).Must(s => s is null || Enum.TryParse<TicketStatus>(s, out _)).WithMessage("Unknown status.");
        RuleFor(q => q.Priority).Must(p => p is null || Enum.TryParse<TicketPriority>(p, out _)).WithMessage("Unknown priority.");
    }
}

internal sealed class ListTicketsHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListTicketsQuery, PagedResult<TicketSummaryDto>>
{
    public async Task<PagedResult<TicketSummaryDto>> Handle(ListTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Tickets.AsNoTracking().VisibleTo(db, currentUser).Search(request.Search);
        if (request.Status is not null)
        {
            var status = Enum.Parse<TicketStatus>(request.Status);
            query = query.Where(t => t.Status == status);
        }

        if (request.Priority is not null)
        {
            var priority = Enum.Parse<TicketPriority>(request.Priority);
            query = query.Where(t => t.Priority == priority);
        }

        if (request.RequestTypeId is { } typeId)
            query = query.Where(t => t.RequestTypeId == typeId);

        if (request.TeamId is { } teamId)
            query = query.Where(t => t.TeamId == teamId);

        if (request.ActiveOnly)
            query = query.Where(t => Assignment.AutoAssigner.ActiveStatuses.Contains(t.Status));

        var userId = currentUser.UserId ?? Guid.Empty;
        query = request.Scope switch
        {
            TicketScope.Mine => query.Where(t => t.AssigneeId == userId),
            TicketScope.MyTeams => query.Where(t => db.Teams.Any(team => team.Id == t.TeamId && team.Members.Any(m => m.UserId == userId))),
            TicketScope.Unassigned => query.Where(t => t.AssigneeId == null),
            _ => query,
        };

        var total = await query.CountAsync(cancellationToken);
        // Unassigned work is oldest-first (first in, first out); other views show the newest first.
        var ordered = request.Scope is TicketScope.Unassigned or TicketScope.MyTeams
            ? query.OrderBy(t => t.CreatedAt)
            : query.OrderByDescending(t => t.CreatedAt);
        var items = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToSummaries(db)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketSummaryDto>(items, request.Page, request.PageSize, total);
    }
}
