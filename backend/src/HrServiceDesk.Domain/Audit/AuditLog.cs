using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Audit;

public enum AuditAction
{
    /// <summary>Someone other than the requester opened a confidential or sensitive case.</summary>
    SensitiveCaseViewed,
    SensitiveAttachmentDownloaded,
    UserCreated,
    UserUpdated,
    UserRolesChanged,
    UserDeactivated,
    PasswordReset,
    RetentionPolicyChanged,
    CaseAnonymized,
}

/// <summary>
/// Who accessed or changed sensitive data, and when. Entries describe the action, never the sensitive
/// values themselves. Case-level history lives in <c>TicketEvent</c>.
/// </summary>
public sealed class AuditLog : Entity, ITenantOwned
{
    public const int SummaryMaxLength = 500;

    private AuditLog() { }

    public AuditLog(Guid tenantId, Guid? userId, AuditAction action, string entityType, Guid? entityId, string summary, DateTimeOffset occurredAt)
    {
        TenantId = tenantId;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Summary = summary.Length > SummaryMaxLength ? summary[..SummaryMaxLength] : summary;
        OccurredAt = occurredAt;
    }

    public Guid TenantId { get; set; }

    /// <summary>The acting user; null for the system (e.g. the retention job).</summary>
    public Guid? UserId { get; private set; }

    public AuditAction Action { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }
}
