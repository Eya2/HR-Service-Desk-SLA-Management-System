using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Users.Commands;

public sealed record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    Guid? ManagerId,
    bool IsActive) : IRequest<Result<UserDetailsDto>>;

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        UserRules.ValidName(RuleFor(c => c.FirstName));
        UserRules.ValidName(RuleFor(c => c.LastName));
        UserRules.ValidRoles(RuleFor(c => c.Roles));
    }
}

internal sealed class UpdateUserHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IRequestHandler<UpdateUserCommand, Result<UserDetailsDto>>
{
    public async Task<Result<UserDetailsDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        var roles = UserRules.ParseRoles(request.Roles);
        var isSelf = user.Id == currentUser.UserId;
        if (isSelf && !request.IsActive)
            return UserErrors.CannotDeactivateSelf;
        if (isSelf && user.HasRole(Role.HrAdmin) && !roles.Contains(Role.HrAdmin))
            return UserErrors.CannotRemoveOwnAdminRole;
        // Keeping an existing SuperAdmin role is fine; granting it is checked.
        var granted = roles.Where(r => !user.HasRole(r)).ToList();
        if (await UserRules.CheckAsync(db, currentUser, granted, request.ManagerId, cancellationToken) is { } error)
            return error;

        user.Rename(request.FirstName, request.LastName);
        user.SetRoles(roles);
        user.SetManager(request.ManagerId);

        if (user.IsActive && !request.IsActive)
        {
            user.Deactivate();
            await SessionRevoker.RevokeAllAsync(db, user.Id, clock.GetUtcNow(), cancellationToken);
        }
        else if (!user.IsActive && request.IsActive)
        {
            user.Activate();
        }

        await db.SaveChangesAsync(cancellationToken);
        return await db.Users.AsNoTracking().Where(u => u.Id == user.Id).ProjectDetails(db).SingleAsync(cancellationToken);
    }
}
