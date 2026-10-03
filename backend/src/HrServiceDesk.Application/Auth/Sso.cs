using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Application.Auth;

/// <summary>Whether an e-mail's domain signs in with SSO (asked by the sign-in page while the e-mail is typed).</summary>
public sealed record SsoDiscoveryDto(bool Enabled, string? DisplayName, bool PasswordLoginDisabled);

public sealed record DiscoverSsoQuery(string Email) : IRequest<SsoDiscoveryDto>;

/// <summary>Starts an SSO sign-in; returns the provider's authorization URL to redirect to.</summary>
public sealed record StartSsoCommand(string Email, string? ReturnUrl, bool RememberMe) : IRequest<Result<string>>;

public sealed record SsoCompletion(AuthSession Session, string ReturnUrl);

/// <summary>Handles the provider's redirect back (code and state, or an error).</summary>
public sealed record CompleteSsoCommand(string? State, string? Code, string? Error) : IRequest<Result<SsoCompletion>>;

public sealed record SsoSettingsDto(
    bool IsEnabled,
    string DisplayName,
    string Authority,
    string? MetadataAddress,
    string ClientId,
    bool HasClientSecret,
    IReadOnlyList<string> EmailDomains,
    bool AutoProvision,
    bool PasswordLoginDisabled,
    string CallbackUrl);

public sealed record GetSsoSettingsQuery : IRequest<SsoSettingsDto?>;

/// <summary>Saves the organisation's SSO settings; a null or empty secret keeps the current one.</summary>
public sealed record SaveSsoSettingsCommand(
    bool IsEnabled,
    string DisplayName,
    string Authority,
    string? MetadataAddress,
    string ClientId,
    string? ClientSecret,
    IReadOnlyList<string> EmailDomains,
    bool AutoProvision,
    bool PasswordLoginDisabled) : IRequest<Result<SsoSettingsDto>>;

/// <summary>Reads the provider's discovery document with the saved settings.</summary>
public sealed record TestSsoCommand : IRequest<Result<SsoProviderInfo>>;

internal static class SsoErrors
{
    public static readonly Error NotConfigured = Error.NotFound("sso.not_configured", "Single sign-on is not set up for this e-mail domain.");
    public static readonly Error Expired = Error.Unauthorized("sso.expired", "The sign-in took too long or was already used. Please try again.");
    public static readonly Error NoAccount = Error.Unauthorized("sso.no_account", "No active account matches your identity. Ask your HR administrator.");
    public static readonly Error DomainMismatch = Error.Unauthorized("sso.domain_mismatch", "This identity does not belong to the organisation.");
    public static readonly Error DomainTaken = Error.Conflict("sso.domain_taken", "One of these e-mail domains is already used by another organisation.");
    public static readonly Error SecretRequired = Error.Validation("sso.secret_required", "The client secret is required.");

    public static Error Failed(string code, string message) => Error.Unauthorized(code, message);
}

internal sealed class SaveSsoSettingsValidator : AbstractValidator<SaveSsoSettingsCommand>
{
    public SaveSsoSettingsValidator()
    {
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(SsoConfiguration.DisplayNameMaxLength);
        RuleFor(c => c.Authority).NotEmpty().MaximumLength(SsoConfiguration.UrlMaxLength);
        RuleFor(c => c.ClientId).NotEmpty().MaximumLength(SsoConfiguration.ClientIdMaxLength);
        RuleFor(c => c.ClientSecret).MaximumLength(500);
        RuleFor(c => c.EmailDomains).NotEmpty();
    }
}

internal sealed partial class SsoHandlers(
    IAppDbContext db,
    ITenantContext tenantContext,
    ISsoProvider provider,
    ISecretProtector protector,
    IPasswordHasher hasher,
    IAppLinks links,
    SessionIssuer sessions,
    TimeProvider clock,
    IOptions<AuthOptions> options,
    ILogger<SsoHandlers> logger)
    : IRequestHandler<DiscoverSsoQuery, SsoDiscoveryDto>,
      IRequestHandler<StartSsoCommand, Result<string>>,
      IRequestHandler<CompleteSsoCommand, Result<SsoCompletion>>,
      IRequestHandler<GetSsoSettingsQuery, SsoSettingsDto?>,
      IRequestHandler<SaveSsoSettingsCommand, Result<SsoSettingsDto>>,
      IRequestHandler<TestSsoCommand, Result<SsoProviderInfo>>
{
    public async Task<SsoDiscoveryDto> Handle(DiscoverSsoQuery request, CancellationToken cancellationToken)
    {
        var configuration = await ForEmailAsync(request.Email, cancellationToken);
        return configuration is null
            ? new SsoDiscoveryDto(false, null, false)
            : new SsoDiscoveryDto(true, configuration.DisplayName, configuration.PasswordLoginDisabled);
    }

    public async Task<Result<string>> Handle(StartSsoCommand request, CancellationToken cancellationToken)
    {
        var configuration = await ForEmailAsync(request.Email, cancellationToken);
        if (configuration is null)
            return SsoErrors.NotConfigured;

        var attempt = SsoLoginAttempt.Start(configuration.TenantId, request.ReturnUrl ?? "/", request.RememberMe, clock.GetUtcNow());
        db.SsoLoginAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            return await provider.BuildAuthorizeUrlAsync(configuration, attempt, links.SsoCallback(), request.Email.Trim(), cancellationToken);
        }
        catch (SsoException ex)
        {
            return SsoErrors.Failed(ex.Code, ex.Message);
        }
    }

    public async Task<Result<SsoCompletion>> Handle(CompleteSsoCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var attempt = string.IsNullOrEmpty(request.State)
            ? null
            : await db.SsoLoginAttempts.SingleOrDefaultAsync(a => a.State == request.State, cancellationToken);
        if (attempt is null || !attempt.TryUse(now))
            return SsoErrors.Expired;
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(request.Error) || string.IsNullOrEmpty(request.Code))
            return SsoErrors.Failed("sso.denied", "The identity provider did not sign you in.");

        var configuration = await db.SsoConfigurations.IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.TenantId == attempt.TenantId && c.IsEnabled, cancellationToken);
        if (configuration is null)
            return SsoErrors.NotConfigured;

        SsoIdentity identity;
        try
        {
            identity = await provider.CompleteAsync(
                configuration, protector.Unprotect(configuration.ProtectedClientSecret), request.Code, attempt, links.SsoCallback(), cancellationToken);
        }
        catch (SsoException ex)
        {
            LogRejected(logger, ex.Code, ex.Message);
            return SsoErrors.Failed(ex.Code, "The identity provider's answer could not be verified.");
        }

        if (!configuration.HandlesEmail(identity.Email))
            return SsoErrors.DomainMismatch;

        var email = User.NormalizeEmail(identity.Email);
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is not null && user.TenantId != configuration.TenantId)
            return SsoErrors.DomainMismatch;
        if (user is null)
        {
            if (!configuration.AutoProvision)
                return SsoErrors.NoAccount;
            user = Provision(identity, email, configuration.TenantId);
        }

        if (!user.IsActive)
            return SsoErrors.NoAccount;
        var tenant = await db.Tenants.SingleAsync(t => t.Id == user.TenantId, cancellationToken);
        if (!tenant.IsActive)
            return AuthErrors.TenantInactive;

        user.RegisterSuccessfulLogin(now);
        var session = sessions.Issue(user, tenant, familyId: null, attempt.RememberMe, out _);
        await db.SaveChangesAsync(cancellationToken);
        return new SsoCompletion(session, attempt.ReturnUrl);
    }

    public async Task<SsoSettingsDto?> Handle(GetSsoSettingsQuery request, CancellationToken cancellationToken)
    {
        var configuration = await db.SsoConfigurations.SingleOrDefaultAsync(cancellationToken);
        return configuration is null ? null : ToDto(configuration);
    }

    public async Task<Result<SsoSettingsDto>> Handle(SaveSsoSettingsCommand request, CancellationToken cancellationToken)
    {
        var configuration = await db.SsoConfigurations.SingleOrDefaultAsync(cancellationToken);
        var isNew = configuration is null;
        configuration ??= SsoConfiguration.Create();
        configuration.Update(
            request.IsEnabled, request.DisplayName, request.Authority, request.MetadataAddress, request.ClientId,
            request.EmailDomains, request.AutoProvision, request.PasswordLoginDisabled, options.Value.SsoAllowInsecureUrls);

        // A domain routes sign-ins to one organisation only.
        var tenantId = tenantContext.TenantId!.Value;
        foreach (var domain in configuration.EmailDomains)
        {
            if (await db.SsoConfigurations.IgnoreQueryFilters().AnyAsync(c => c.TenantId != tenantId && c.EmailDomains.Contains(domain), cancellationToken))
                return SsoErrors.DomainTaken;
        }

        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            configuration.SetClientSecret(protector.Protect(request.ClientSecret.Trim()));
        else if (string.IsNullOrEmpty(configuration.ProtectedClientSecret))
            return SsoErrors.SecretRequired;

        if (isNew)
            db.SsoConfigurations.Add(configuration);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(configuration);
    }

    public async Task<Result<SsoProviderInfo>> Handle(TestSsoCommand request, CancellationToken cancellationToken)
    {
        var configuration = await db.SsoConfigurations.SingleOrDefaultAsync(cancellationToken);
        if (configuration is null)
            return SsoErrors.NotConfigured;
        try
        {
            return await provider.DescribeAsync(configuration, cancellationToken);
        }
        catch (SsoException ex)
        {
            return Error.DomainRule(ex.Code, ex.Message);
        }
    }

    private async Task<SsoConfiguration?> ForEmailAsync(string email, CancellationToken cancellationToken)
    {
        var at = (email ?? string.Empty).Trim().LastIndexOf('@');
        if (at <= 0)
            return null;
        var domain = email!.Trim()[(at + 1)..].ToLowerInvariant();
        return await db.SsoConfigurations.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsEnabled && c.EmailDomains.Contains(domain), cancellationToken);
    }

    /// <summary>First sign-in of someone the provider knows but the desk does not: an Employee account.</summary>
    private User Provision(SsoIdentity identity, string email, Guid tenantId)
    {
        var local = email[..email.IndexOf('@', StringComparison.Ordinal)];
        var parts = (identity.Name ?? local.Replace('.', ' ')).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = Truncate(identity.FirstName ?? (parts.Length > 0 ? parts[0] : local));
        var last = Truncate(identity.LastName ?? (parts.Length > 1 ? parts[1] : "-"));
        var user = User.Create(email, Capitalize(first), Capitalize(last), [Role.Employee]);
        user.TenantId = tenantId;
        user.SetPasswordHash(hasher.Hash(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));
        db.Users.Add(user);
        LogProvisioned(logger, email);
        return user;
    }

    private static string Truncate(string value) => value.Length > User.NameMaxLength ? value[..User.NameMaxLength] : value;

    private static string Capitalize(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private SsoSettingsDto ToDto(SsoConfiguration c) => new(
        c.IsEnabled, c.DisplayName, c.Authority, c.MetadataAddress, c.ClientId, !string.IsNullOrEmpty(c.ProtectedClientSecret),
        c.EmailDomains, c.AutoProvision, c.PasswordLoginDisabled, links.SsoCallback());

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSO sign-in rejected ({Code}): {Reason}")]
    private static partial void LogRejected(ILogger logger, string code, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "SSO: account created on first sign-in for {Email}")]
    private static partial void LogProvisioned(ILogger logger, string email);
}
