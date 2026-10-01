namespace HrServiceDesk.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a single tenant. The persistence layer applies a global
/// query filter on <see cref="TenantId"/> and stamps it on insert.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
