using HrServiceDesk.Application.Abstractions;

namespace HrServiceDesk.Api.Common;

/// <summary>Resolves the tenant from the authenticated user's <c>tenant_id</c> claim.</summary>
internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string TenantClaimType = "tenant_id";

    public Guid? TenantId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst(TenantClaimType)?.Value;
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }
}
