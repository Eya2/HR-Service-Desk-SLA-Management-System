namespace HrServiceDesk.Application.Abstractions;

/// <summary>The tenant the current operation runs for. Resolved from the JWT claim or the API key.</summary>
public interface ITenantContext
{
    /// <summary>The current tenant, or <c>null</c> when the caller is anonymous or a cross-tenant SuperAdmin.</summary>
    Guid? TenantId { get; }
}
