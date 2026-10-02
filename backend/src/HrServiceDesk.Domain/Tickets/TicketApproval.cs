using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Domain.Tickets;

public enum ApprovalDecision
{
    Pending,
    Approved,
    Rejected,

    /// <summary>Not needed any more: an earlier step rejected, or the case was cancelled.</summary>
    Skipped,
}

/// <summary>
/// One approval step copied onto a case at submission. Either a specific person must decide
/// (<see cref="ApproverUserId"/>, e.g. the requester's manager) or anyone holding <see cref="ApproverRole"/>.
/// </summary>
public sealed class TicketApproval : Entity, ITenantOwned
{
    public const int CommentMaxLength = 2000;

    private TicketApproval() { }

    internal TicketApproval(Guid ticketId, int stepOrder, string stepName, Role approverRole, Guid? approverUserId)
    {
        TicketId = ticketId;
        StepOrder = stepOrder;
        StepName = stepName;
        ApproverRole = approverRole;
        ApproverUserId = approverUserId;
    }

    public Guid TenantId { get; set; }

    public Guid TicketId { get; private set; }

    public int StepOrder { get; private set; }

    public string StepName { get; private set; } = string.Empty;

    public Role ApproverRole { get; private set; }

    public Guid? ApproverUserId { get; private set; }

    public ApprovalDecision Decision { get; private set; } = ApprovalDecision.Pending;

    public Guid? DecidedById { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? Comment { get; private set; }

    /// <summary>Whether this user may decide this step (ignores whether it is the current step).</summary>
    public bool IsApprover(Guid userId, IReadOnlyCollection<Role> roles) =>
        ApproverUserId is { } specific ? specific == userId : roles.Contains(ApproverRole);

    internal void Redact() => Comment = null;

    internal void Decide(ApprovalDecision decision, Guid? deciderId, string? comment, DateTimeOffset now)
    {
        Decision = decision;
        DecidedById = deciderId;
        DecidedAt = now;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }
}
