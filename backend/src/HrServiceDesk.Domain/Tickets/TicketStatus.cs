namespace HrServiceDesk.Domain.Tickets;

/// <summary>Lifecycle states. Transitions between them are governed by the status machine (phase 4).</summary>
public enum TicketStatus
{
    New,
    PendingApproval,
    Open,
    InProgress,
    WaitingOnEmployee,
    Resolved,
    Closed,
    Reopened,
    Rejected,
    Cancelled,
}
