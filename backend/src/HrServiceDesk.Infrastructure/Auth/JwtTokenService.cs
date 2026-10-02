using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HrServiceDesk.Infrastructure.Auth;

internal sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions, IOptions<AuthOptions> authOptions, TimeProvider clock)
    : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user, Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(tenant);

        var jwt = jwtOptions.Value;
        var now = clock.GetUtcNow();
        var expires = now + authOptions.Value.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(AppClaims.Subject, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AppClaims.Email, user.Email),
            new(AppClaims.Name, user.FullName),
            new(AppClaims.Tenant, tenant.Id.ToString()),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(AppClaims.Role, r.ToString())));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
