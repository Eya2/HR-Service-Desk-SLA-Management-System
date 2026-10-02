using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

public sealed record UserProfile(Guid Id, string Email, string FirstName, string LastName, string FullName, string[] Roles, Guid TenantId, string TenantName);

public sealed record Session(string AccessToken, DateTimeOffset ExpiresAt, UserProfile User);

/// <summary>A signed-in session: the access token plus the raw refresh token taken from the Set-Cookie header.</summary>
public sealed record SignedIn(Session Session, string RefreshToken, SetCookieHeaderValue Cookie);

public static class AuthHelpers
{
    public const string RefreshCookieName = "hrdesk_rt";

    public static Task<HttpResponseMessage> PostLoginAsync(this HttpClient client, string email, string password = DemoUsers.Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static async Task<SignedIn> LoginAsync(this HttpClient client, string email, string password = DemoUsers.Password)
    {
        var response = await client.PostLoginAsync(email, password);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadSessionAsync(response);
    }

    public static async Task<SignedIn> ReadSessionAsync(HttpResponseMessage response)
    {
        var session = (await response.Content.ReadFromJsonAsync<Session>())!;
        var cookie = RefreshCookie(response) ?? throw new InvalidOperationException("No refresh cookie was set.");
        return new SignedIn(session, cookie.Value.ToString(), cookie);
    }

    public static SetCookieHeaderValue? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()).FirstOrDefault(c => c.Name == RefreshCookieName)
            : null;

    public static Task<HttpResponseMessage> PostRefreshAsync(this HttpClient client, string? refreshToken) =>
        client.SendAsync(WithCookie(HttpMethod.Post, "/api/auth/refresh", refreshToken));

    public static Task<HttpResponseMessage> PostLogoutAsync(this HttpClient client, string? refreshToken) =>
        client.SendAsync(WithCookie(HttpMethod.Post, "/api/auth/logout", refreshToken));

    public static HttpClient Authorize(this HttpClient client, SignedIn signedIn)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.Session.AccessToken);
        return client;
    }

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static HttpRequestMessage WithCookie(HttpMethod method, string url, string? refreshToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (refreshToken is not null)
            request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");
        return request;
    }
}
