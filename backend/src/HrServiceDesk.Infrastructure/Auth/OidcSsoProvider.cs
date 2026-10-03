using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace HrServiceDesk.Infrastructure.Auth;

/// <summary>
/// OpenID Connect authorization code flow with PKCE (works with Microsoft Entra ID, Google, Okta, Keycloak…).
/// Discovery documents and signing keys are cached per provider and refreshed when a key is unknown.
/// </summary>
internal sealed class OidcSsoProvider(IHttpClientFactory httpClients, IOptions<AuthOptions> options) : ISsoProvider
{
    public const string ClientName = "oidc";

    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _metadata = new();

    public async Task<string> BuildAuthorizeUrlAsync(
        SsoConfiguration configuration, SsoLoginAttempt attempt, string redirectUri, string? loginHint, CancellationToken cancellationToken)
    {
        var metadata = await MetadataAsync(configuration, refresh: false, cancellationToken);
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = configuration.ClientId,
            ["response_type"] = "code",
            ["response_mode"] = "query",
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid profile email",
            ["state"] = attempt.State,
            ["nonce"] = attempt.Nonce,
            ["code_challenge"] = attempt.CodeChallenge,
            ["code_challenge_method"] = "S256",
            ["login_hint"] = loginHint,
        };
        var separator = metadata.AuthorizationEndpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return metadata.AuthorizationEndpoint + separator + string.Join('&',
            query.Where(p => !string.IsNullOrEmpty(p.Value)).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
    }

    public async Task<SsoIdentity> CompleteAsync(
        SsoConfiguration configuration, string clientSecret, string code, SsoLoginAttempt attempt, string redirectUri, CancellationToken cancellationToken)
    {
        var metadata = await MetadataAsync(configuration, refresh: false, cancellationToken);
        using var response = await httpClients.CreateClient(ClientName).PostAsync(
            metadata.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = configuration.ClientId,
                ["client_secret"] = clientSecret,
                ["code_verifier"] = attempt.CodeVerifier,
            }),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SsoException("sso.token_failed", $"The token endpoint answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrEmpty(tokens?.IdToken))
            throw new SsoException("sso.token_failed", "The provider returned no ID token.");

        var result = await ValidateAsync(tokens.IdToken, configuration, metadata);
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // The provider may have rotated its keys since they were cached.
            metadata = await MetadataAsync(configuration, refresh: true, cancellationToken);
            result = await ValidateAsync(tokens.IdToken, configuration, metadata);
        }

        if (!result.IsValid)
            throw new SsoException("sso.invalid_token", result.Exception?.Message ?? "The ID token is not valid.");

        var claims = result.Claims;
        if (!claims.TryGetValue("nonce", out var nonce) || nonce as string != attempt.Nonce)
            throw new SsoException("sso.nonce_mismatch", "The ID token was not issued for this sign-in.");

        var email = Text(claims, "email") ?? Text(claims, "preferred_username") ?? Text(claims, "upn");
        if (email is null || !email.Contains('@', StringComparison.Ordinal))
            throw new SsoException("sso.no_email", "The provider did not share an e-mail address (add the 'email' optional claim).");

        return new SsoIdentity(Text(claims, "sub") ?? string.Empty, email, Text(claims, "given_name"), Text(claims, "family_name"), Text(claims, "name"));
    }

    public async Task<SsoProviderInfo> DescribeAsync(SsoConfiguration configuration, CancellationToken cancellationToken)
    {
        var metadata = await MetadataAsync(configuration, refresh: true, cancellationToken);
        return new SsoProviderInfo(metadata.Issuer, metadata.AuthorizationEndpoint, metadata.TokenEndpoint);
    }

    private static Task<TokenValidationResult> ValidateAsync(string idToken, SsoConfiguration configuration, OpenIdConnectConfiguration metadata) =>
        new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = metadata.Issuer,
            ValidAudience = configuration.ClientId,
            IssuerSigningKeys = metadata.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        });

    private async Task<OpenIdConnectConfiguration> MetadataAsync(SsoConfiguration configuration, bool refresh, CancellationToken cancellationToken)
    {
        var manager = _metadata.GetOrAdd(configuration.EffectiveMetadataAddress, address => new ConfigurationManager<OpenIdConnectConfiguration>(
            address,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(httpClients.CreateClient(ClientName)) { RequireHttps = !options.Value.SsoAllowInsecureUrls }));
        if (refresh)
            manager.RequestRefresh();
        try
        {
            return await manager.GetConfigurationAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Any discovery failure becomes a clear SSO error.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _metadata.TryRemove(configuration.EffectiveMetadataAddress, out _);
            throw new SsoException("sso.metadata_unavailable", $"The provider's configuration could not be read from {configuration.EffectiveMetadataAddress}: {ex.Message}");
        }
    }

    private static string? Text(IDictionary<string, object> claims, string name) =>
        claims.TryGetValue(name, out var value) && value is string text && text.Length > 0 ? text : null;

    private sealed record TokenResponse([property: JsonPropertyName("id_token")] string? IdToken);
}
