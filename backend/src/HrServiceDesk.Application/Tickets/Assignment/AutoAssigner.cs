using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Assignment;

/// <summary>
/// Assigns a case within its team according to the team's strategy, once HR can work on it
/// (at submission without approval, or when the last approval opens it). The caller saves.
/// </summary>
internal sealed class AutoAssigner(IAppDbContext db, TimeProvider clock)
{
    private static readonly Role[] StaffRoles = [.. Policies.RolesByPolicy[Policies.CanWorkTickets]];

    public async Task AssignAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        if (ticket.TeamId is not { } teamId || ticket.AssigneeId is not null || ticket.IsFinal)
            return;

        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null)
            return;

        var memberIds = team.OrderedMemberIds;
        // Only active members who still hold an HR role can receive cases.
        var eligible = await db.Users
            .Where(u => memberIds.Contains(u.Id) && u.IsActive && u.Roles.Any(r => StaffRoles.Contains(r)))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (eligible.Count == 0)
            return;

        var load = await db.Tickets
            .Where(t => t.AssigneeId != null && eligible.Contains(t.AssigneeId.Value) && ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.AssigneeId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        if (team.PickAssignee(eligible, load) is { } assignee)
            ticket.Assign(assignee, actorId: null, clock.GetUtcNow());
    }

    /// <summary>Statuses that count towards an agent's workload.</summary>
    public static readonly TicketStatus[] ActiveStatuses =
    [
        TicketStatus.New, TicketStatus.Open, TicketStatus.InProgress, TicketStatus.WaitingOnEmployee, TicketStatus.Reopened,
    ];
}
