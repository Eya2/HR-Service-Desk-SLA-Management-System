using System.Security.Cryptography;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Users;

/// <summary>
/// An organisation's single sign-on through an OpenID Connect provider (Microsoft Entra ID, Google, Okta,
/// Keycloak…). People whose e-mail domain is listed sign in at the provider; the desk maps them to their
/// account by e-mail. The client secret is stored encrypted.
/// </summary>
public sealed class SsoConfiguration : Entity, ITenantOwned, IAuditable
{
    public const int DisplayNameMaxLength = 60;
    public const int UrlMaxLength = 500;
    public const int ClientIdMaxLength = 200;
    public const int MaxDomains = 10;

    private SsoConfiguration() { }

    public Guid TenantId { get; set; }

    public bool IsEnabled { get; private set; }

    /// <summary>Shown on the sign-in button ("Continue with Microsoft").</summary>
    public string DisplayName { get; private set; } = "Microsoft";

    /// <summary>The issuer, e.g. <c>https://login.microsoftonline.com/&lt;tenant-id&gt;/v2.0</c>.</summary>
    public string Authority { get; private set; } = string.Empty;

    /// <summary>Where the discovery document is read; defaults to the authority's well-known address.</summary>
    public string? MetadataAddress { get; private set; }

    public string ClientId { get; private set; } = string.Empty;

    public string ProtectedClientSecret { get; private set; } = string.Empty;

    /// <summary>Lower-case e-mail domains routed to this provider.</summary>
    public string[] EmailDomains { get; private set; } = [];

    /// <summary>Creates an Employee account on first sign-in when none exists.</summary>
    public bool AutoProvision { get; private set; }

    /// <summary>People of these domains must use SSO; HR Admins keep their password as a break-glass access.</summary>
    public bool PasswordLoginDisabled { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string EffectiveMetadataAddress =>
        MetadataAddress ?? $"{Authority.TrimEnd('/')}/.well-known/openid-configuration";

    public static SsoConfiguration Create() => new();

    public void Update(
        bool isEnabled, string displayName, string authority, string? metadataAddress, string clientId,
        IEnumerable<string> emailDomains, bool autoProvision, bool passwordLoginDisabled, bool allowInsecureUrls)
    {
        displayName = (displayName ?? string.Empty).Trim();
        if (displayName.Length is 0 or > DisplayNameMaxLength)
            throw new DomainException("sso.invalid_display_name", $"The button name must be 1 to {DisplayNameMaxLength} characters.");
        clientId = (clientId ?? string.Empty).Trim();
        if (clientId.Length is 0 or > ClientIdMaxLength)
            throw new DomainException("sso.invalid_client_id", "The client ID is required.");

        var domains = (emailDomains ?? [])
            .Select(d => d.Trim().TrimStart('@').ToLowerInvariant())
            .Where(d => d.Length > 0)
            .Distinct()
            .ToArray();
        if (domains.Length is 0 or > MaxDomains || domains.Any(d => !d.Contains('.', StringComparison.Ordinal) || d.Contains(' ', StringComparison.Ordinal)))
            throw new DomainException("sso.invalid_domains", $"List 1 to {MaxDomains} e-mail domains, such as acme.com.");

        IsEnabled = isEnabled;
        DisplayName = displayName;
        Authority = ValidateUrl(authority, allowInsecureUrls, "sso.invalid_authority");
        MetadataAddress = string.IsNullOrWhiteSpace(metadataAddress) ? null : ValidateUrl(metadataAddress, allowInsecureUrls, "sso.invalid_metadata");
        ClientId = clientId;
        EmailDomains = domains;
        AutoProvision = autoProvision;
        PasswordLoginDisabled = passwordLoginDisabled;
    }

    public void SetClientSecret(string protectedSecret) => ProtectedClientSecret = protectedSecret;

    public bool HandlesEmail(string email)
    {
        var at = email.LastIndexOf('@');
        return at > 0 && EmailDomains.Contains(email[(at + 1)..].ToLowerInvariant());
    }

    private static string ValidateUrl(string? url, bool allowInsecure, string code)
    {
        url = (url ?? string.Empty).Trim();
        if (url.Length is 0 or > UrlMaxLength || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(allowInsecure && uri.Scheme == Uri.UriSchemeHttp)))
            throw new DomainException(code, "The address must be an absolute https URL.");
        return url.TrimEnd('/');
    }
}

/// <summary>
/// One SSO sign-in in progress, kept on the server: the random <c>state</c> that comes back from the provider,
/// the <c>nonce</c> expected in the ID token and the PKCE verifier. Single use, valid 10 minutes.
/// </summary>
public sealed class SsoLoginAttempt : Entity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private SsoLoginAttempt() { }

    public Guid TenantId { get; private set; }

    public string State { get; private set; } = string.Empty;

    public string Nonce { get; private set; } = string.Empty;

    public string CodeVerifier { get; private set; } = string.Empty;

    public string ReturnUrl { get; private set; } = "/";

    public bool RememberMe { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>The PKCE challenge sent to the provider (S256 of the verifier).</summary>
    public string CodeChallenge => Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(CodeVerifier)));

    public static SsoLoginAttempt Start(Guid tenantId, string returnUrl, bool rememberMe, DateTimeOffset now) => new()
    {
        TenantId = tenantId,
        State = Random(32),
        Nonce = Random(32),
        CodeVerifier = Random(48),
        ReturnUrl = SafeReturnUrl(returnUrl),
        RememberMe = rememberMe,
        ExpiresAt = now + Lifetime,
    };

    /// <summary>Marks the attempt used; false when it already was or has expired.</summary>
    public bool TryUse(DateTimeOffset now)
    {
        if (UsedAt is not null || now > ExpiresAt)
            return false;
        UsedAt = now;
        return true;
    }

    /// <summary>Only paths of this app, never another site (open redirect).</summary>
    public static string SafeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            ? url
            : "/";

    private static string Random(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
