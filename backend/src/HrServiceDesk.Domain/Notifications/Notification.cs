using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Notifications;

public enum NotificationType
{
    TicketCreated,
    TicketAssigned,
    StatusChanged,
    ApprovalRequested,
    CommentAdded,
    SlaAtRisk,
    SlaBreached,
    Escalated,
}

/// <summary>An in-app message for one user (also e-mailed). Texts never include confidential case content.</summary>
public sealed class Notification : Entity, ITenantOwned
{
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 1000;

    private Notification() { }

    public Notification(Guid tenantId, Guid userId, NotificationType type, string title, string message, Guid? ticketId, DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        UserId = userId;
        Type = type;
        Title = title.Length > TitleMaxLength ? title[..TitleMaxLength] : title;
        Message = message.Length > MessageMaxLength ? message[..MessageMaxLength] : message;
        TicketId = ticketId;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; set; }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public Guid? TicketId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
