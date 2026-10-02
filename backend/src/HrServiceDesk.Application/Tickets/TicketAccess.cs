using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets;

/// <summary>
/// Who may see and act on which cases. Visibility is applied inside the database query, so a case that
/// is not visible behaves exactly like one that does not exist (404).
/// </summary>
internal static class TicketAccess
{
    private static readonly Role[] Staff = [.. Policies.RolesByPolicy[Policies.CanWorkTickets]];

    /// <summary>HR staff who handle cases (HR Officer, Payroll Specialist, HR Admin).</summary>
    public static bool IsStaff(ICurrentUser user) => Staff.Any(user.IsInRole);

    /// <summary>Staff and auditors see every non-confidential case and internal comments.</summary>
    public static bool SeesAllCases(ICurrentUser user) => IsStaff(user) || user.IsInRole(Role.Auditor);

    /// <summary>Members of the organisation's restricted HR group(s), who alone (with the requester) see confidential cases.</summary>
    public static IQueryable<Guid> ConfidentialGroupMembers(IAppDbContext db) =>
        db.Teams.Where(t => t.IsConfidentialGroup).SelectMany(t => t.Members.Select(m => m.UserId));

    public static Task<bool> IsInConfidentialGroupAsync(IAppDbContext db, Guid userId, CancellationToken cancellationToken) =>
        ConfidentialGroupMembers(db).AnyAsync(id => id == userId, cancellationToken);

    /// <summary>
    /// Requesters see their own cases; staff and auditors the organisation's non-confidential cases;
    /// managers their direct reports' non-confidential cases; a named approver the non-confidential cases
    /// they must decide. Confidential cases: the requester and the restricted HR group only.
    /// </summary>
    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> tickets, IAppDbContext db, ICurrentUser user)
    {
        var userId = user.UserId ?? Guid.Empty;
        var seesAll = SeesAllCases(user);
        var isManager = user.IsInRole(Role.Manager);
        var confidentialGroup = ConfidentialGroupMembers(db);
        return tickets.Where(t =>
            t.RequesterId == userId
            || (t.IsConfidential
                ? confidentialGroup.Contains(userId)
                : seesAll
                  || (isManager && db.Users.Any(u => u.Id == t.RequesterId && u.ManagerId == userId))
                  || db.TicketApprovals.Any(a => a.TicketId == t.Id && a.ApproverUserId == userId)));
    }

    /// <summary>The status-machine roles the user holds on this case.</summary>
    public static TransitionActor ActorFor(Ticket ticket, ICurrentUser user)
    {
        var actor = TransitionActor.None;
        if (ticket.RequesterId == user.UserId)
            actor |= TransitionActor.Requester;
        if (IsStaff(user))
            actor |= TransitionActor.Agent;
        return actor;
    }

    /// <summary>The requester and HR staff take part in a case; auditors only read.</summary>
    public static bool CanParticipate(Ticket ticket, ICurrentUser user) =>
        ticket.RequesterId == user.UserId || IsStaff(user);
}
