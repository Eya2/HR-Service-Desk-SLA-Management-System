using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Auth.Commands;

/// <summary>Changes the caller's password and signs out all of their sessions.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).MustSatisfyPasswordPolicy()
            .NotEqual(c => c.CurrentPassword).WithMessage("The new password must differ from the current one.");
    }
}

internal sealed class ChangePasswordHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    TimeProvider clock) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return AuthErrors.NotAuthenticated;

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return AuthErrors.NotAuthenticated;

        if (hasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordCheck.Failed)
            return AuthErrors.WrongCurrentPassword;

        user.SetPasswordHash(hasher.Hash(request.NewPassword));
        await SessionRevoker.RevokeAllAsync(db, user.Id, clock.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
