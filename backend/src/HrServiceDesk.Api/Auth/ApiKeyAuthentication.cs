using System.Security.Claims;
using System.Text.Encodings.Web;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Integration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Api.Auth;

/// <summary>
/// Authenticates external systems with the <c>X-Api-Key</c> header. The principal carries the key's
/// organisation (tenant), its service account as subject and one <c>scope</c> claim per granted scope.
/// Only the integration endpoints accept this scheme.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAppDbContext db,
    TimeProvider clock) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ScopeClaim = "scope";
    public const string KeyIdClaim = "api_key_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var presented = values.ToString();
        if (ApiKey.PrefixOf(presented) is not { } prefix)
            return AuthenticateResult.Fail("Malformed API key.");

        var key = await db.ApiKeys.IgnoreQueryFilters().SingleOrDefaultAsync(k => k.Prefix == prefix, Context.RequestAborted);
        if (key is null || !key.IsActive || !key.Matches(presented))
            return AuthenticateResult.Fail("Invalid or revoked API key.");

        if (key.MarkUsed(clock.GetUtcNow()))
            await db.SaveChangesAsync(Context.RequestAborted);

        var claims = new List<Claim>
        {
            new(AppClaims.Subject, key.ServiceUserId.ToString()),
            new(AppClaims.Tenant, key.TenantId.ToString()),
            new(AppClaims.Name, key.Name),
            new(KeyIdClaim, key.Id.ToString()),
        };
        claims.AddRange(key.Scopes.Select(s => new Claim(ScopeClaim, s)));
        var identity = new ClaimsIdentity(claims, SchemeName, AppClaims.Name, AppClaims.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"{SchemeName} header=\"{HeaderName}\"";
        return base.HandleChallengeAsync(properties);
    }
}

internal static class IntegrationPolicies
{
    public const string TicketsRead = "Integration.TicketsRead";
    public const string TicketsWrite = "Integration.TicketsWrite";
    public const string RateLimit = "integration";
}
