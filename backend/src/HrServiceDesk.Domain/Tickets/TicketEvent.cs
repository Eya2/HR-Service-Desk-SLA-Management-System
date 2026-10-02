using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Tickets;

public enum TicketEventType
{
    Created,
    StatusChanged,
    PriorityChanged,
    DetailsUpdated,
    CommentAdded,
    AttachmentAdded,
    Assigned,
    ApprovalRequested,
    ApprovalDecided,
    SlaStateChanged,
    Escalated,
}

/// <summary>
/// One entry of a case's audit trail. Events are append-only; <see cref="Data"/> holds the details as
/// JSON (e.g. <c>{"from":"Open","to":"Resolved"}</c>). Internal events are hidden from the employee.
/// </summary>
public sealed class TicketEvent : Entity, ITenantOwned
{
    private TicketEvent() { }

    internal TicketEvent(Guid ticketId, TicketEventType type, Guid? actorId, DateTimeOffset occurredAt, string data, bool isInternal)
    {
        TicketId = ticketId;
        Type = type;
        ActorId = actorId;
        OccurredAt = occurredAt;
        Data = data;
        IsInternal = isInternal;
    }

    public Guid TenantId { get; set; }

    public Guid TicketId { get; private set; }

    public TicketEventType Type { get; private set; }

    /// <summary>The user who caused the event, or null for the system.</summary>
    public Guid? ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Data { get; private set; } = "{}";

    public bool IsInternal { get; private set; }

    /// <summary>Keeps the facts (type, statuses, dates) and drops free text (titles, reasons, comments, file names).</summary>
    internal void Redact(Guid previousRequester, Guid formerEmployee)
    {
        if (ActorId == previousRequester)
            ActorId = formerEmployee;

        using var document = System.Text.Json.JsonDocument.Parse(Data);
        var kept = document.RootElement.EnumerateObject()
            .Where(p => p.Name is "from" or "to" or "decision" or "action" or "trigger" or "isInternal" or "reference" or "teamId" or "fromTeamId")
            .ToDictionary(p => p.Name, p => p.Value.Clone());
        Data = System.Text.Json.JsonSerializer.Serialize(kept);
    }
}
