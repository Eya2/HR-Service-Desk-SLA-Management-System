using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;

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

    /// <summary>
    /// Interim rule until the restricted HR group (phase 9): confidential cases are visible to the requester
    /// and HR Admins only.
    /// </summary>
    public static bool SeesConfidential(ICurrentUser user) => user.IsInRole(Role.HrAdmin);

    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> tickets, ICurrentUser user)
    {
        var userId = user.UserId ?? Guid.Empty;
        var seesAll = SeesAllCases(user);
        var seesConfidential = SeesConfidential(user);
        return tickets.Where(t => t.RequesterId == userId || (seesAll && (!t.IsConfidential || seesConfidential)));
    }

    /// <summary>The requester and HR staff take part in a case; auditors only read.</summary>
    public static bool CanParticipate(Ticket ticket, ICurrentUser user) =>
        ticket.RequesterId == user.UserId || IsStaff(user);
}
