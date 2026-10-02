using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Infrastructure.Auth;
using HrServiceDesk.Domain.Integration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HrServiceDesk.Api.Auth;

internal static class AuthSetup
{
    public const string AuthRateLimitPolicy = "auth";

    public static IServiceCollection AddApiAuth(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

        // Configured from JwtOptions at resolve time, so test hosts can override the settings.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                // Browsers cannot set headers on WebSocket connections: the hub gets the token from the query string.
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = token;
                        return Task.CompletedTask;
                    },
                };
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaims.Name,
                    RoleClaimType = AppClaims.Role,
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var (policy, roles) in Policies.RolesByPolicy)
                options.AddPolicy(policy, p => p.RequireRole(roles.Select(r => r.ToString())));

            // Integration endpoints: API keys only, each scope checked (a user's token is never accepted there).
            options.AddPolicy(IntegrationPolicies.TicketsRead, p => p
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ApiKeyAuthenticationHandler.ScopeClaim, ApiScopes.TicketsRead));
            options.AddPolicy(IntegrationPolicies.TicketsWrite, p => p
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ApiKeyAuthenticationHandler.ScopeClaim, ApiScopes.TicketsWrite));

            // Secure by default: every endpoint requires a signed-in user unless marked [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, http =>
            {
                var settings = http.RequestServices.GetRequiredService<IConfiguration>().GetSection("RateLimiting:Auth");
                return RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.GetValue("PermitLimit", 10),
                        Window = TimeSpan.FromSeconds(settings.GetValue("WindowSeconds", 60)),
                        QueueLimit = 0,
                    });
            });
            // Per key: an integration loop cannot starve the desk.
            options.AddPolicy(IntegrationPolicies.RateLimit, http =>
            {
                var settings = http.RequestServices.GetRequiredService<IConfiguration>().GetSection("RateLimiting:Integration");
                return RateLimitPartition.GetFixedWindowLimiter(
                    http.Request.Headers[ApiKeyAuthenticationHandler.HeaderName].ToString() is { Length: > 12 } key ? key[..12] : "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.GetValue("PermitLimit", 300),
                        Window = TimeSpan.FromSeconds(settings.GetValue("WindowSeconds", 60)),
                        QueueLimit = 0,
                    });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests. Please wait and try again.",
                        Extensions = { ["code"] = "rate_limited" },
                    },
                });
            };
        });

        return services;
    }
}
