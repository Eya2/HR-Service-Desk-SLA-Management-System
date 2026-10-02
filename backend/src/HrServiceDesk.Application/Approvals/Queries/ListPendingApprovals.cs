using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Approvals.Queries;

/// <summary>
/// The caller's approval queue: current steps assigned to them by name (e.g. as manager) or to one of
/// their roles, excluding their own requests, oldest first.
/// </summary>
public sealed record ListPendingApprovalsQuery : IRequest<IReadOnlyList<PendingApprovalDto>>;

internal sealed class ListPendingApprovalsHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListPendingApprovalsQuery, IReadOnlyList<PendingApprovalDto>>
{
    public async Task<IReadOnlyList<PendingApprovalDto>> Handle(ListPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var roles = currentUser.Roles.ToList();
        var confidentialGroup = Tickets.TicketAccess.ConfidentialGroupMembers(db);

        var query =
            from a in db.TicketApprovals.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on a.TicketId equals t.Id
            join type in db.RequestTypes.AsNoTracking() on t.RequestTypeId equals type.Id
            join requester in db.Users.AsNoTracking() on t.RequesterId equals requester.Id
            where t.Status == TicketStatus.PendingApproval
                  && a.Decision == ApprovalDecision.Pending
                  // only the current step: no earlier step is still pending
                  && !db.TicketApprovals.Any(p => p.TicketId == a.TicketId && p.Decision == ApprovalDecision.Pending && p.StepOrder < a.StepOrder)
                  && t.RequesterId != userId
                  && (a.ApproverUserId == userId || (a.ApproverUserId == null && roles.Contains(a.ApproverRole)))
                  // confidential cases are decided only within the restricted HR group
                  && (!t.IsConfidential || confidentialGroup.Contains(userId))
            orderby t.CreatedAt
            select new PendingApprovalDto(
                a.Id,
                t.Id,
                t.Reference,
                t.Title,
                type.Name,
                requester.FirstName + " " + requester.LastName,
                a.StepName,
                a.StepOrder,
                db.TicketApprovals.Count(x => x.TicketId == t.Id),
                t.CreatedAt);

        return await query.ToListAsync(cancellationToken);
    }
}
