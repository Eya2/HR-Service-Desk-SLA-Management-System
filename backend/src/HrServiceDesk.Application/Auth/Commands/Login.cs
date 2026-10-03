using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Application.Auth.Commands;

/// <summary>Signs in. <see cref="RememberMe"/> keeps the session across browser restarts.</summary>
public sealed record LoginCommand(string Email, string Password, bool RememberMe = false) : IRequest<Result<AuthSession>>;

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
    }
}

internal sealed class LoginHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    SessionIssuer sessions,
    TimeProvider clock,
    IOptions<AuthOptions> options) : IRequestHandler<LoginCommand, Result<AuthSession>>
{
    // Verified when the e-mail is unknown, so both paths cost the same (no user enumeration by timing).
    private static string? _dummyHash;

    public async Task<Result<AuthSession>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var user = await FindUserAsync(request.Email, cancellationToken);

        // SSO required for the domain: checked before the password so the answer reveals nothing about it.
        // HR Admins keep their password as a break-glass access.
        if (await SsoRequiredAsync(request.Email, cancellationToken) && user?.Roles.Contains(Role.HrAdmin) != true)
            return AuthErrors.SsoRequired;

        if (user is null)
        {
            hasher.Verify(_dummyHash ??= hasher.Hash(Guid.NewGuid().ToString()), request.Password);
            return AuthErrors.InvalidCredentials;
        }

        if (user.IsLockedOut(now))
            return AuthErrors.LockedOut;

        var check = hasher.Verify(user.PasswordHash, request.Password);
        if (check == PasswordCheck.Failed)
        {
            user.RegisterFailedLogin(now, options.Value.MaxFailedLoginAttempts, options.Value.LockoutDuration);
            await db.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidCredentials;
        }

        if (!user.IsActive)
            return AuthErrors.InvalidCredentials;

        var tenant = await db.Tenants.SingleAsync(t => t.Id == user.TenantId, cancellationToken);
        if (!tenant.IsActive)
            return AuthErrors.TenantInactive;

        if (check == PasswordCheck.SuccessRehashNeeded)
            user.SetPasswordHash(hasher.Hash(request.Password));

        user.RegisterSuccessfulLogin(now);
        var session = sessions.Issue(user, tenant, familyId: null, request.RememberMe, out _);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task<bool> SsoRequiredAsync(string email, CancellationToken cancellationToken)
    {
        var at = (email ?? string.Empty).Trim().LastIndexOf('@');
        if (at <= 0)
            return false;
        var domain = email!.Trim()[(at + 1)..].ToLowerInvariant();
        return await db.SsoConfigurations.IgnoreQueryFilters()
            .AnyAsync(c => c.IsEnabled && c.PasswordLoginDisabled && c.EmailDomains.Contains(domain), cancellationToken);
    }

    private async Task<User?> FindUserAsync(string email, CancellationToken cancellationToken)
    {
        string normalized;
        try
        {
            normalized = User.NormalizeEmail(email);
        }
        catch (DomainException)
        {
            return null;
        }

        return await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }
}
