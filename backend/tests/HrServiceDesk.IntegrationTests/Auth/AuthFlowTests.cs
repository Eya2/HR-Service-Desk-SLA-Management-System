using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace HrServiceDesk.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class AuthFlowTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    [Fact]
    public async Task Login_returns_access_token_profile_and_a_locked_down_refresh_cookie()
    {
        var client = _api.CreateApiClient();

        var signedIn = await client.LoginAsync(DemoUsers.AcmeHrAdmin);

        signedIn.Session.AccessToken.Should().NotBeNullOrWhiteSpace();
        signedIn.Session.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
        signedIn.Session.User.Email.Should().Be(DemoUsers.AcmeHrAdmin);
        signedIn.Session.User.Roles.Should().BeEquivalentTo("Employee", "HrAdmin");
        signedIn.Session.User.TenantName.Should().Be("Acme Tunisie");

        signedIn.Cookie.HttpOnly.Should().BeTrue();
        signedIn.Cookie.Secure.Should().BeTrue();
        signedIn.Cookie.SameSite.Should().Be(SameSiteMode.Strict);
        signedIn.Cookie.Path.ToString().Should().Be("/api/auth");
    }

    [Fact]
    public async Task Login_is_case_insensitive_on_email()
    {
        var response = await _api.CreateApiClient().PostLoginAsync(DemoUsers.AcmeEmployee.ToUpperInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(DemoUsers.AcmeEmployee, "Wrong-Passw0rd!")]
    [InlineData("nobody@acme.example", DemoUsers.Password)]
    [InlineData("not-an-email", DemoUsers.Password)]
    public async Task Wrong_credentials_get_the_same_generic_401(string email, string password)
    {
        var response = await _api.CreateApiClient().PostLoginAsync(email, password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).Should().Be("auth.invalid_credentials");
        AuthHelpers.RefreshCookie(response).Should().BeNull();
    }

    [Fact]
    public async Task Login_without_body_fields_is_a_validation_problem()
    {
        var response = await _api.CreateApiClient().PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Email").And.Contain("Password");
    }

    [Fact]
    public async Task Account_is_locked_after_five_failed_attempts()
    {
        var client = _api.CreateApiClient();
        var email = await TestUsers.CreateAsync(_api, "lockout");

        for (var i = 0; i < 5; i++)
            (await client.PostLoginAsync(email, "Wrong-Passw0rd!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var response = await client.PostLoginAsync(email, TestUsers.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).Should().Be("auth.locked_out");
    }

    [Fact]
    public async Task Protected_endpoints_require_a_valid_token()
    {
        var client = _api.CreateApiClient();

        var anonymous = await client.GetAsync("/api/auth/me");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        anonymous.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var signedIn = await _api.CreateApiClient().LoginAsync(DemoUsers.AcmeEmployee);
        var parts = signedIn.Session.AccessToken.Split('.');
        var forged = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";
        var client = _api.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_returns_the_profile_from_the_token()
    {
        var client = _api.CreateApiClient();
        client.Authorize(await client.LoginAsync(DemoUsers.GlobexEmployee));

        var me = await client.GetFromJsonAsync<UserProfile>("/api/auth/me");

        me!.Email.Should().Be(DemoUsers.GlobexEmployee);
        me.TenantName.Should().Be("Globex France");
        me.Roles.Should().Equal("Employee");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reuse_revokes_the_whole_family()
    {
        var client = _api.CreateApiClient();
        var first = await client.LoginAsync(DemoUsers.AcmeEmployee);

        var refreshed = await client.PostRefreshAsync(first.RefreshToken);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await AuthHelpers.ReadSessionAsync(refreshed);
        second.RefreshToken.Should().NotBe(first.RefreshToken);
        second.Session.AccessToken.Should().NotBe(first.Session.AccessToken);

        // Replaying the rotated token looks like theft: it fails and kills the successor too.
        var replay = await client.PostRefreshAsync(first.RefreshToken);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await replay.ProblemCodeAsync()).Should().Be("auth.refresh_token_reused");

        var successor = await client.PostRefreshAsync(second.RefreshToken);
        successor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_without_or_with_unknown_cookie_is_401_and_clears_the_cookie()
    {
        var client = _api.CreateApiClient();

        (await client.PostRefreshAsync(null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var unknown = await client.PostRefreshAsync("forged-token");
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AuthHelpers.RefreshCookie(unknown)!.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = _api.CreateApiClient();
        var signedIn = await client.LoginAsync(DemoUsers.AcmeManager);

        (await client.PostLogoutAsync(signedIn.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostRefreshAsync(signedIn.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_without_a_session_still_succeeds()
    {
        (await _api.CreateApiClient().PostLogoutAsync(null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Change_password_enforces_policy_and_signs_out_other_sessions()
    {
        var email = await TestUsers.CreateAsync(_api, "changepw");
        var client = _api.CreateApiClient();
        var signedIn = await client.LoginAsync(email, TestUsers.Password);
        client.Authorize(signedIn);

        var wrongCurrent = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Wrong-Passw0rd!", newPassword = "Another-Passw0rd!" });
        wrongCurrent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrongCurrent.ProblemCodeAsync()).Should().Be("auth.wrong_current_password");

        var weak = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestUsers.Password, newPassword = "short" });
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await weak.Content.ReadAsStringAsync()).Should().Contain("NewPassword");

        var ok = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestUsers.Password, newPassword = "Another-Passw0rd!" });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostRefreshAsync(signedIn.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostLoginAsync(email, TestUsers.Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostLoginAsync(email, "Another-Passw0rd!")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
