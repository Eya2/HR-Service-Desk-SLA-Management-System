using HrServiceDesk.Application.Common.Results;

namespace HrServiceDesk.Application.Users;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("user.not_found", "User not found.");
    public static readonly Error EmailTaken = Error.Conflict("user.email_taken", "A user with this e-mail already exists.");
    public static readonly Error ManagerNotFound = Error.Validation("user.manager_not_found", "The manager does not exist in this organisation.");
    public static readonly Error SuperAdminForbidden = Error.Forbidden("user.super_admin_forbidden", "Only a SuperAdmin can grant the SuperAdmin role.");
    public static readonly Error CannotDeactivateSelf = Error.DomainRule("user.cannot_deactivate_self", "You cannot deactivate your own account.");
    public static readonly Error CannotRemoveOwnAdminRole = Error.DomainRule("user.cannot_remove_own_admin_role", "You cannot remove your own HR Admin role.");
}
