using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Approvals;
using HrServiceDesk.Application.Approvals.Commands;
using HrServiceDesk.Application.Approvals.Queries;
using HrServiceDesk.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Approval queue and decisions.</summary>
[Authorize(Policy = Policies.CanApprove)]
public sealed class ApprovalsController : ApiControllerBase
{
    public sealed record DecisionRequest(bool Approve, string? Comment);

    /// <summary>Cases waiting for the caller's decision, oldest first.</summary>
    [HttpGet("api/approvals/pending")]
    [ProducesResponseType<IReadOnlyList<PendingApprovalDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingApprovalDto>>> Pending(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListPendingApprovalsQuery(), cancellationToken));

    /// <summary>Approves or rejects the current step of a case. Rejecting requires a comment.</summary>
    [HttpPost("api/tickets/{ticketId:guid}/approvals/{approvalId:guid}/decision")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Decide(Guid ticketId, Guid approvalId, DecisionRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new DecideApprovalCommand(ticketId, approvalId, request.Approve, request.Comment), cancellationToken));
}
