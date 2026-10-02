using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed partial class PasswordResetTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Remember_me_makes_the_refresh_cookie_persistent()
    {
        var client = postgres.Api.CreateApiClient();

        var session = await client.PostAsJsonAsync("/api/auth/login", new { email = DemoUsers.AcmeEmployee, password = DemoUsers.Password });
        AuthHelpers.RefreshCookie(session)!.Expires.Should().BeNull("without remember me the cookie ends with the browser session");

        var remembered = await client.PostAsJsonAsync("/api/auth/login", new { email = DemoUsers.AcmeEmployee, password = DemoUsers.Password, rememberMe = true });
        var cookie = AuthHelpers.RefreshCookie(remembered)!;
        cookie.Expires.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));

        // The choice survives token rotation.
        var refreshed = await client.PostRefreshAsync(cookie.Value.ToString());
        AuthHelpers.RefreshCookie(refreshed)!.Expires.Should().NotBeNull();
    }

    [Fact]
    public async Task A_forgotten_password_is_reset_with_a_single_use_link_that_expires()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, DateTimeOffset.UtcNow);
        var email = await TestUsers.CreateAsync(api, "forgot");
        var client = api.CreateApiClient();
        await client.LoginAsync(email, TestUsers.Password);

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@acme.example" })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = email.ToUpperInvariant() })).StatusCode.Should().Be(HttpStatusCode.Accepted);

        api.Emails.Sent.Should().NotContain(m => m.To == "nobody@acme.example", "nothing tells whether an account exists");
        var mail = api.Emails.Sent.Last(m => m.To == email);
        mail.Subject.Should().Contain("Reset your HR Service Desk password");
        var token = Uri.UnescapeDataString(TokenPattern().Match(mail.Body).Groups[1].Value);

        var weak = await client.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = "short" });
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = "Brand-New-Passw0rd!" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostLoginAsync(email, TestUsers.Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostLoginAsync(email, "Brand-New-Passw0rd!")).StatusCode.Should().Be(HttpStatusCode.OK);

        var reused = await client.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword = "Another-Passw0rd!" });
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await reused.ProblemCodeAsync()).Should().Be("auth.invalid_reset_link");

        // A new link expires after one hour.
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var late = Uri.UnescapeDataString(TokenPattern().Match(api.Emails.Sent.Last(m => m.To == email).Body).Groups[1].Value);
        api.Clock.Advance(TimeSpan.FromMinutes(61));
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { email, token = late, newPassword = "Another-Passw0rd!" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [GeneratedRegex(@"token=([^\s&]+)")]
    private static partial Regex TokenPattern();
}
