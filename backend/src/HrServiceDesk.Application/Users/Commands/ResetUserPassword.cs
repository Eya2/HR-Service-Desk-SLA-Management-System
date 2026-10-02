using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Users.Commands;

/// <summary>An HR Admin sets a new password for a user, which signs the user out everywhere.</summary>
public sealed record ResetUserPasswordCommand(Guid Id, string NewPassword) : IRequest<Result>;

internal sealed class ResetUserPasswordValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordValidator() => RuleFor(c => c.NewPassword).MustSatisfyPasswordPolicy();
}

internal sealed class ResetUserPasswordHandler(IAppDbContext db, IPasswordHasher hasher, TimeProvider clock)
    : IRequestHandler<ResetUserPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        user.SetPasswordHash(hasher.Hash(request.NewPassword));
        user.RegisterSuccessfulLogin(clock.GetUtcNow()); // clears any lockout
        await SessionRevoker.RevokeAllAsync(db, user.Id, clock.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
