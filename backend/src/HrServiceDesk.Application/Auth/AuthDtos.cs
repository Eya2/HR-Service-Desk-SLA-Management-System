namespace HrServiceDesk.Application.Auth;

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    IReadOnlyList<string> Roles,
    Guid TenantId,
    string TenantName);

/// <summary>Result of a sign-in or refresh. The refresh token travels in an HttpOnly cookie, never in the body.</summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool IsPersistent,
    UserProfileDto User);
