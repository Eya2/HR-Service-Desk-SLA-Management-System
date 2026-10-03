using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;

namespace HrServiceDesk.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class SingleSignOnTests(PostgresFixture postgres)
{
    private sealed record Discovery(bool Enabled, string? DisplayName, bool PasswordLoginDisabled);

    private sealed record Settings(bool IsEnabled, string Authority, string ClientId, bool HasClientSecret, string[] EmailDomains, string CallbackUrl);

    private sealed record ProviderInfo(string Issuer, string AuthorizationEndpoint, string TokenEndpoint);

    private static async Task<HttpClient> AdminAsync(ApiFactory api, string email = DemoUsers.AcmeHrAdmin)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static object SettingsBody(SsoApiFactory api, bool autoProvision = false, bool passwordLoginDisabled = false, bool isEnabled = true, string? secret = FakeIdentityProvider.ClientSecret, string domain = "acme.example") => new
    {
        isEnabled,
        displayName = "Microsoft",
        authority = api.Idp.Authority,
        metadataAddress = (string?)null,
        clientId = FakeIdentityProvider.ClientId,
        clientSecret = secret,
        emailDomains = new[] { domain },
        autoProvision,
        passwordLoginDisabled,
    };

    /// <summary>Sets up SSO for Acme, runs the test, then switches SSO off again (the database is shared).</summary>
    private static async Task WithSsoAsync(SsoApiFactory api, Func<HttpClient, Task> test, bool autoProvision = false, bool passwordLoginDisabled = false)
    {
        var admin = await AdminAsync(api);
        (await admin.PutAsJsonAsync("/api/settings/sso", SettingsBody(api, autoProvision, passwordLoginDisabled))).EnsureSuccessStatusCode();
        try
        {
            await test(admin);
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/settings/sso", SettingsBody(api, isEnabled: false, secret: null))).EnsureSuccessStatusCode();
        }
    }

    /// <summary>Plays the browser: start → provider sign-in → callback. Returns the final redirect.</summary>
    private static async Task<HttpResponseMessage> SignInAsync(SsoApiFactory api, FakeSignIn signIn, string loginEmail, string returnUrl = "/portal/requests")
    {
        var browser = api.CreateBrowser();
        var start = await browser.GetAsync($"/api/auth/sso/start?email={Uri.EscapeDataString(loginEmail)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        start.StatusCode.Should().Be(HttpStatusCode.Redirect);
        start.Headers.Location!.ToString().Should().StartWith($"{api.Idp.Authority}/authorize");

        var callback = api.Idp.Authorize(start.Headers.Location!.ToString(), signIn);
        return await browser.GetAsync(callback);
    }

    private static string? SsoError(HttpResponseMessage response) =>
        QueryHelpers.ParseQuery(response.Headers.Location!.Query).TryGetValue("ssoError", out var error) ? error.ToString() : null;

    [Fact]
    public async Task An_employee_signs_in_with_the_identity_provider_and_lands_where_she_was_going()
    {
        await using var api = new SsoApiFactory(postgres.ConnectionString);
        await WithSsoAsync(api, async _ =>
        {
            var discovery = await api.CreateApiClient().GetFromJsonAsync<Discovery>($"/api/auth/sso/discover?email={DemoUsers.AcmeEmployee}");
            discovery.Should().Be(new Discovery(true, "Microsoft", false));
            (await api.CreateApiClient().GetFromJsonAsync<Discovery>("/api/auth/sso/discover?email=someone@globex.example"))!.Enabled.Should().BeFalse();

            var done = await SignInAsync(api, new FakeSignIn(DemoUsers.AcmeEmployee), DemoUsers.AcmeEmployee);

            done.StatusCode.Should().Be(HttpStatusCode.Redirect);
            done.Headers.Location!.ToString().Should().EndWith("/sso/complete?returnUrl=%2Fportal%2Frequests");
            var cookie = AuthHelpers.RefreshCookie(done);
            cookie.Should().NotBeNull();
            var session = await AuthHelpers.ReadSessionAsync(await api.CreateApiClient().PostRefreshAsync(cookie!.Value.ToString()));
            session.Session.User.Email.Should().Be(DemoUsers.AcmeEmployee);
        });
    }

    [Fact]
    public async Task A_new_joiner_gets_an_employee_account_only_when_auto_provisioning_is_on()
    {
        await using var api = new SsoApiFactory(postgres.ConnectionString);
        var email = $"new.joiner.{Guid.NewGuid():N}@acme.example";

        await WithSsoAsync(api, async _ =>
        {
            var refused = await SignInAsync(api, new FakeSignIn(email, "New", "Joiner"), email);
            SsoError(refused).Should().Be("sso.no_account");
        });

        await WithSsoAsync(api, async admin =>
        {
            var done = await SignInAsync(api, new FakeSignIn(email, "Nour", "Ben Ali"), email);
            var session = await AuthHelpers.ReadSessionAsync(await api.CreateApiClient().PostRefreshAsync(AuthHelpers.RefreshCookie(done)!.Value.ToString()));
            session.Session.User.FullName.Should().Be("Nour Ben Ali");
            session.Session.User.Roles.Should().Equal("Employee");
        }, autoProvision: true);
    }

    [Theory]
    [InlineData("audience")]
    [InlineData("nonce")]
    [InlineData("key")]
    [InlineData("domain")]
    public async Task Answers_that_fail_validation_are_refused(string tamper)
    {
        await using var api = new SsoApiFactory(postgres.ConnectionString);
        await WithSsoAsync(api, async _ =>
        {
            var signIn = tamper switch
            {
                "audience" => new FakeSignIn(DemoUsers.AcmeEmployee) { Audience = "another-app" },
                "nonce" => new FakeSignIn(DemoUsers.AcmeEmployee) { Nonce = "replayed" },
                "key" => new FakeSignIn(DemoUsers.AcmeEmployee) { SignWithUnknownKey = true },
                _ => new FakeSignIn(DemoUsers.GlobexEmployee),
            };

            var response = await SignInAsync(api, signIn, DemoUsers.AcmeEmployee);

            SsoError(response).Should().Be(tamper switch
            {
                "nonce" => "sso.nonce_mismatch",
                "domain" => "sso.domain_mismatch",
                _ => "sso.invalid_token",
            });
            AuthHelpers.RefreshCookie(response).Should().BeNull();
        });
    }

    [Fact]
    public async Task A_state_is_used_once_and_provider_errors_end_the_attempt()
    {
        await using var api = new SsoApiFactory(postgres.ConnectionString);
        await WithSsoAsync(api, async _ =>
        {
            var browser = api.CreateBrowser();
            var start = await browser.GetAsync($"/api/auth/sso/start?email={DemoUsers.AcmeEmployee}");
            var callback = api.Idp.Authorize(start.Headers.Location!.ToString(), new FakeSignIn(DemoUsers.AcmeEmployee));
            (await browser.GetAsync(callback)).Headers.Location!.ToString().Should().Contain("/sso/complete");

            SsoError(await browser.GetAsync(callback)).Should().Be("sso.expired");
            SsoError(await browser.GetAsync("/api/auth/sso/callback?state=unknown&code=x")).Should().Be("sso.expired");

            var denied = await browser.GetAsync($"/api/auth/sso/start?email={DemoUsers.AcmeEmployee}");
            var state = QueryHelpers.ParseQuery(denied.Headers.Location!.Query)["state"].ToString();
            SsoError(await browser.GetAsync($"/api/auth/sso/callback?state={state}&error=access_denied")).Should().Be("sso.denied");
        });
    }

    [Fact]
    public async Task When_sso_is_required_passwords_only_work_for_hr_admins()
    {
        await using var api = new SsoApiFactory(postgres.ConnectionString);
        await WithSsoAsync(api, async _ =>
        {
            var client = api.CreateApiClient();
            var employee = await client.PostLoginAsync(DemoUsers.AcmeEmployee);
            employee.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await employee.ProblemCodeAsync()).Should().Be("auth.sso_required");
            (await (await client.PostLoginAsync("nobody@acme.example")).ProblemCodeAsync()).Should().Be("auth.sso_required", "unknown and known people get the same answer");
            (await client.PostLoginAsync(DemoUsers.AcmeHrAdmin)).StatusCode.Should().Be(HttpStatusCode.OK, "break-glass access");
            (await client.PostLoginAsync(DemoUsers.GlobexEmployee)).StatusCode.Should().Be(HttpStatusCode.OK, "other organisations are not affected");
        }, passwordLoginDisabled: true);
    }

    [Fact]
    public async Task Admins_manage_the_settings_the_secret_never_comes_back_and_domains_belong_to_one_organisation()
    {
        await using var api = new SsoApiFactory(postgres.ConnectionString);
        await WithSsoAsync(api, async admin =>
        {
            var settings = await admin.GetFromJsonAsync<Settings>("/api/settings/sso");
            settings!.HasClientSecret.Should().BeTrue();
            settings.CallbackUrl.Should().EndWith("/api/auth/sso/callback");
            (await admin.GetStringAsync("/api/settings/sso")).Should().NotContain(FakeIdentityProvider.ClientSecret);

            var info = await (await admin.PostAsync("/api/settings/sso/test", null)).Content.ReadFromJsonAsync<ProviderInfo>();
            info!.Issuer.Should().Be(api.Idp.Authority);

            var globexAdmin = await AdminAsync(api, DemoUsers.GlobexHrAdmin);
            (await globexAdmin.PutAsJsonAsync("/api/settings/sso", SettingsBody(api, domain: "acme.example"))).StatusCode.Should().Be(HttpStatusCode.Conflict);

            var employee = api.CreateApiClient();
            employee.Authorize(await employee.LoginAsync(DemoUsers.AcmeEmployee));
            (await employee.GetAsync("/api/settings/sso")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        });
    }
}
