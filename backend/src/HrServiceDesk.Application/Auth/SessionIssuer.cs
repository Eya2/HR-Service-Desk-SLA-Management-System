using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Users;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Application.Auth;

/// <summary>Creates an access token and a new refresh token (optionally continuing a token family).</summary>
internal sealed class SessionIssuer(IAppDbContext db, ITokenService tokens, TimeProvider clock, IOptions<AuthOptions> options)
{
    public AuthSession Issue(User user, Tenant tenant, Guid? familyId, out RefreshToken refreshToken)
    {
        var now = clock.GetUtcNow();
        var access = tokens.CreateAccessToken(user, tenant);
        var raw = tokens.GenerateRefreshToken();
        refreshToken = RefreshToken.Issue(user, tokens.HashRefreshToken(raw), now, options.Value.RefreshTokenLifetime, familyId);
        db.RefreshTokens.Add(refreshToken);

        return new AuthSession(access.Token, access.ExpiresAt, raw, refreshToken.ExpiresAt, ToProfile(user, tenant));
    }

    public static UserProfileDto ToProfile(User user, Tenant tenant) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.FullName,
        user.Roles.Select(r => r.ToString()).ToList(),
        tenant.Id,
        tenant.Name);
}
