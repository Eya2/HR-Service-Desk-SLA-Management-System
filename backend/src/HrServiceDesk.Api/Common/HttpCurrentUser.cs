using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Api.Common;

/// <summary>Reads the caller from the validated access token (<c>sub</c> and <c>role</c> claims).</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User.FindFirst(AppClaims.Subject)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsInRole(Role role) => httpContextAccessor.HttpContext?.User.IsInRole(role.ToString()) == true;

    public IReadOnlyCollection<Role> Roles =>
        httpContextAccessor.HttpContext?.User.FindAll(AppClaims.Role)
            .Select(c => Enum.TryParse<Role>(c.Value, out var role) ? role : (Role?)null)
            .OfType<Role>()
            .ToList() ?? [];
}
