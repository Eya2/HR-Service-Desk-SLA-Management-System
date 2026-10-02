using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;

namespace HrServiceDesk.Api.Common;

/// <summary>Resolves the tenant from the authenticated user's <c>tenant_id</c> claim.</summary>
internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? TenantId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst(AppClaims.Tenant)?.Value;
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }
}
