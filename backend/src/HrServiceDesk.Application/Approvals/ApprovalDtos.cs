namespace HrServiceDesk.Application.Approvals;

/// <summary>A case waiting for the caller's decision.</summary>
public sealed record PendingApprovalDto(
    Guid ApprovalId,
    Guid TicketId,
    string Reference,
    string Title,
    string RequestTypeName,
    string RequesterName,
    string StepName,
    int StepOrder,
    int StepCount,
    DateTimeOffset SubmittedAt);
