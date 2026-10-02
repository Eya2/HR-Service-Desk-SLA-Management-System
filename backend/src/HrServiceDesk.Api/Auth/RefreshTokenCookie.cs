namespace HrServiceDesk.Api.Auth;

/// <summary>
/// The refresh token lives in an HttpOnly, Secure, SameSite=Strict cookie scoped to <c>/api/auth</c>:
/// scripts cannot read it and it is only sent to the auth endpoints.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "hrdesk_rt";
    private const string Path = "/api/auth";

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(expiresAt: null));

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    private static CookieOptions Options(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true, // browsers accept Secure cookies on http://localhost
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
