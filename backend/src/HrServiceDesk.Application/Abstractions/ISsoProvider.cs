using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Application.Abstractions;

/// <summary>Who the identity provider says signed in (from a validated ID token).</summary>
public sealed record SsoIdentity(string Subject, string Email, string? FirstName, string? LastName, string? Name);

/// <summary>What the provider's discovery document announces (for the admin's "test connection").</summary>
public sealed record SsoProviderInfo(string Issuer, string AuthorizationEndpoint, string TokenEndpoint);

/// <summary>A sign-in the provider refused or that failed validation; <see cref="Code"/> is safe to show.</summary>
public sealed class SsoException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>OpenID Connect authorization code flow with PKCE against an organisation's provider.</summary>
public interface ISsoProvider
{
    Task<string> BuildAuthorizeUrlAsync(SsoConfiguration configuration, SsoLoginAttempt attempt, string redirectUri, string? loginHint, CancellationToken cancellationToken);

    /// <summary>Redeems the code and validates the ID token (issuer, audience, signature, lifetime, nonce).</summary>
    Task<SsoIdentity> CompleteAsync(SsoConfiguration configuration, string clientSecret, string code, SsoLoginAttempt attempt, string redirectUri, CancellationToken cancellationToken);

    Task<SsoProviderInfo> DescribeAsync(SsoConfiguration configuration, CancellationToken cancellationToken);
}
