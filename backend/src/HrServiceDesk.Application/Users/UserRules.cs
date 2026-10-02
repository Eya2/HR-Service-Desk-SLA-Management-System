using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Users;

internal static class UserRules
{
    public static void ValidName<T>(IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength);

    public static void ValidRoles<T>(IRuleBuilder<T, IReadOnlyList<string>> rule) =>
        rule.NotEmpty().WithMessage("At least one role is required.")
            .Must(roles => roles.All(r => Enum.TryParse<Role>(r, ignoreCase: false, out _)))
            .WithMessage($"Roles must be among: {string.Join(", ", Enum.GetNames<Role>())}.");

    public static List<Role> ParseRoles(IEnumerable<string> roles) => roles.Select(Enum.Parse<Role>).ToList();

    /// <summary>Checks role grants and the manager reference shared by create and update.</summary>
    public static async Task<Error?> CheckAsync(
        IAppDbContext db, ICurrentUser currentUser, IReadOnlyCollection<Role> roles, Guid? managerId, CancellationToken cancellationToken)
    {
        if (roles.Contains(Role.SuperAdmin) && !currentUser.IsInRole(Role.SuperAdmin))
            return UserErrors.SuperAdminForbidden;

        // The tenant query filter guarantees the manager belongs to the caller's organisation.
        if (managerId is { } id && !await db.Users.AnyAsync(u => u.Id == id, cancellationToken))
            return UserErrors.ManagerNotFound;

        return null;
    }

    public static IQueryable<UserDetailsDto> ProjectDetails(this IQueryable<User> users, IAppDbContext db) =>
        users.Select(u => new UserDetailsDto(
            u.Id,
            u.Email,
            u.FirstName,
            u.LastName,
            u.Roles.Select(r => r.ToString()).ToList(),
            u.ManagerId,
            db.Users.Where(m => m.Id == u.ManagerId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefault(),
            u.IsActive,
            u.LastLoginAt,
            u.CreatedAt));
}
