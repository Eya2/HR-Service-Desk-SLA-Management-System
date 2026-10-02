using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Audit;

namespace HrServiceDesk.Application.Audit;

/// <summary>Writes access and change records for sensitive data. Summaries never contain the sensitive values.</summary>
internal sealed class AuditTrail(IAppDbContext db, ICurrentUser currentUser, ITenantContext tenantContext, TimeProvider clock)
{
    /// <summary>Adds an entry to be saved with the caller's changes.</summary>
    public void Add(AuditAction action, string entityType, Guid? entityId, string summary, Guid? tenantId = null) =>
        db.AuditLogs.Add(new AuditLog(
            tenantId ?? tenantContext.TenantId ?? Guid.Empty, currentUser.UserId, action, entityType, entityId, summary, clock.GetUtcNow()));

    /// <summary>Records a read access right away (reads have no save of their own).</summary>
    public async Task WriteAsync(AuditAction action, string entityType, Guid? entityId, string summary, CancellationToken cancellationToken)
    {
        Add(action, entityType, entityId, summary);
        await db.SaveChangesAsync(cancellationToken);
    }
}
