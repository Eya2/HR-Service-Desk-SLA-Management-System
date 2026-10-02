using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Users.Commands;

public sealed record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    Guid? ManagerId,
    string Password) : IRequest<Result<UserDetailsDto>>;

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(User.EmailMaxLength);
        UserRules.ValidName(RuleFor(c => c.FirstName));
        UserRules.ValidName(RuleFor(c => c.LastName));
        UserRules.ValidRoles(RuleFor(c => c.Roles));
        RuleFor(c => c.Password).MustSatisfyPasswordPolicy();
    }
}

internal sealed class CreateUserHandler(IAppDbContext db, ICurrentUser currentUser, IPasswordHasher hasher)
    : IRequestHandler<CreateUserCommand, Result<UserDetailsDto>>
{
    public async Task<Result<UserDetailsDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var roles = UserRules.ParseRoles(request.Roles);
        if (await UserRules.CheckAsync(db, currentUser, roles, request.ManagerId, cancellationToken) is { } error)
            return error;

        var email = User.NormalizeEmail(request.Email);
        // E-mails are unique platform-wide because they are the login.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken))
            return UserErrors.EmailTaken;

        var user = User.Create(email, request.FirstName, request.LastName, roles);
        user.SetManager(request.ManagerId);
        user.SetPasswordHash(hasher.Hash(request.Password));
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return await db.Users.AsNoTracking().Where(u => u.Id == user.Id).ProjectDetails(db).SingleAsync(cancellationToken);
    }
}
