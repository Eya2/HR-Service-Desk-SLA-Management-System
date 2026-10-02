using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Issues signed access tokens and opaque refresh tokens.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user, Tenant tenant);

    /// <summary>A new random, URL-safe refresh token. Only its hash is ever stored.</summary>
    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
