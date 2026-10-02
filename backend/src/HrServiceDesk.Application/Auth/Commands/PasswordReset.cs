using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Audit;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Audit;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Application.Auth.Commands;

/// <summary>Sends a password reset link if the address belongs to an active account. The answer is always the same.</summary>
public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;

/// <summary>Sets a new password with a reset link's token; ends every session and unlocks the account.</summary>
public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>;

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
}

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(c => c.Token).NotEmpty().MaximumLength(200);
        RuleFor(c => c.NewPassword).MustSatisfyPasswordPolicy();
    }
}

internal sealed class PasswordResetHandlers(
    IAppDbContext db,
    ITokenService tokens,
    IPasswordHasher hasher,
    IEmailOutbox outbox,
    IAppLinks links,
    AuditTrail audit,
    TimeProvider clock,
    IOptions<AuthOptions> options)
    : IRequestHandler<ForgotPasswordCommand, Result>,
      IRequestHandler<ResetPasswordCommand, Result>
{
    private static readonly Error InvalidLink = Error.Validation("auth.invalid_reset_link", "This link is invalid or has expired. Ask for a new one.");

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await FindAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
            return Result.Success();

        var now = clock.GetUtcNow();
        // Only the newest link works.
        foreach (var previous in await db.PasswordResetTokens.IgnoreQueryFilters()
                     .Where(t => t.UserId == user.Id && t.UsedAt == null).ToListAsync(cancellationToken))
            previous.MarkUsed(now);

        var raw = tokens.GenerateRefreshToken();
        db.PasswordResetTokens.Add(new PasswordResetToken(user, tokens.HashRefreshToken(raw), now, options.Value.PasswordResetLifetime));
        await db.SaveChangesAsync(cancellationToken);

        var minutes = (int)options.Value.PasswordResetLifetime.TotalMinutes;
        await outbox.SendAsync(new EmailMessage(
            user.Email,
            "Reset your HR Service Desk password",
            $"Hello {user.FirstName},\n\nUse this link within {minutes} minutes to choose a new password:\n{links.PasswordReset(user.Email, raw)}\n\n"
            + "If you did not ask for it, ignore this e-mail: your password stays the same.\n\n— HR Service Desk"),
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await FindAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
            return InvalidLink;

        var now = clock.GetUtcNow();
        var hash = tokens.HashRefreshToken(request.Token);
        var token = await db.PasswordResetTokens.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == user.Id, cancellationToken);
        if (token is null || !token.IsUsable(now))
            return InvalidLink;

        token.MarkUsed(now);
        user.SetPasswordHash(hasher.Hash(request.NewPassword));
        user.RegisterSuccessfulLogin(now); // clears a lockout
        foreach (var session in await db.RefreshTokens.IgnoreQueryFilters()
                     .Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(cancellationToken))
            session.Revoke(now);
        audit.Add(AuditAction.PasswordReset, "User", user.Id, "Password reset with an e-mailed link", user.TenantId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<User?> FindAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            var normalized = User.NormalizeEmail(email);
            return await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}
