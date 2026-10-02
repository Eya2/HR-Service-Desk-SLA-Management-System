using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Approvals.Queries;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Application.Tickets.Queries;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Team.Queries;

/// <summary>Non-confidential requests of the caller's direct reports, newest first.</summary>
public sealed record ListTeamTicketsQuery(string? Status, int Page = 1, int PageSize = 20) : IRequest<PagedResult<TicketSummaryDto>>;

internal sealed class ListTeamTicketsValidator : AbstractValidator<ListTeamTicketsQuery>
{
    public ListTeamTicketsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Status).Must(s => s is null || Enum.TryParse<TicketStatus>(s, out _)).WithMessage("Unknown status.");
    }
}

internal sealed class ListTeamTicketsHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListTeamTicketsQuery, PagedResult<TicketSummaryDto>>
{
    public async Task<PagedResult<TicketSummaryDto>> Handle(ListTeamTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = TeamScope.Tickets(db, currentUser);
        if (request.Status is not null)
        {
            var status = Enum.Parse<TicketStatus>(request.Status);
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToSummaries(db).ToListAsync(cancellationToken);
        return new PagedResult<TicketSummaryDto>(items, request.Page, request.PageSize, total);
    }
}

public sealed record GetTeamStatsQuery : IRequest<TeamStatsDto>;

internal sealed class GetTeamStatsHandler(IAppDbContext db, ICurrentUser currentUser, ISender sender, TimeProvider clock)
    : IRequestHandler<GetTeamStatsQuery, TeamStatsDto>
{
    public async Task<TeamStatsDto> Handle(GetTeamStatsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var tickets = TeamScope.Tickets(db, currentUser);
        var since = clock.GetUtcNow().AddDays(-30);

        var byStatus = await tickets.GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var pending = await sender.Send(new ListPendingApprovalsQuery(), cancellationToken);

        return new TeamStatsDto(
            TeamSize: await db.Users.CountAsync(u => u.ManagerId == userId && u.IsActive, cancellationToken),
            OpenCases: byStatus.Where(s => !TicketStatusMachine.IsTerminal(s.Status) && s.Status != TicketStatus.Resolved).Sum(s => s.Count),
            SubmittedLast30Days: await tickets.CountAsync(t => t.CreatedAt >= since, cancellationToken),
            PendingMyApproval: pending.Count,
            ByStatus: byStatus.OrderBy(s => s.Status).Select(s => new StatusCountDto(s.Status.ToString(), s.Count)).ToList());
    }
}

internal static class TeamScope
{
    public static IQueryable<Ticket> Tickets(IAppDbContext db, ICurrentUser user)
    {
        var userId = user.UserId ?? Guid.Empty;
        return db.Tickets.AsNoTracking().Where(t =>
            !t.IsConfidential && db.Users.Any(u => u.Id == t.RequesterId && u.ManagerId == userId));
    }
}
