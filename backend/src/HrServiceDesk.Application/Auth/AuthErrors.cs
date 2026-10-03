using HrServiceDesk.Application.Common.Results;

namespace HrServiceDesk.Application.Auth;

public static class AuthErrors
{
    // Deliberately vague: it must not reveal whether the e-mail exists.
    public static readonly Error InvalidCredentials = Error.Unauthorized("auth.invalid_credentials", "Invalid e-mail or password.");
    public static readonly Error LockedOut = Error.Unauthorized("auth.locked_out", "Too many failed attempts. Try again later.");
    public static readonly Error TenantInactive = Error.Unauthorized("auth.tenant_inactive", "This organisation is not active.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("auth.invalid_refresh_token", "The session has expired. Please sign in again.");
    public static readonly Error RefreshTokenReused = Error.Unauthorized("auth.refresh_token_reused", "The session was revoked for security reasons. Please sign in again.");
    public static readonly Error NotAuthenticated = Error.Unauthorized("auth.not_authenticated", "Authentication is required.");
    public static readonly Error SsoRequired = Error.Unauthorized("auth.sso_required", "Your organisation signs in with single sign-on.");
    public static readonly Error WrongCurrentPassword = Error.Validation("auth.wrong_current_password", "The current password is incorrect.");
}
