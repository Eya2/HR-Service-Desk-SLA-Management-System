using System.Net;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class RateLimitTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Auth_endpoints_are_rate_limited_with_a_problem_response()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, new Dictionary<string, string>
        {
            ["RateLimiting:Auth:PermitLimit"] = "3",
            ["RateLimiting:Auth:WindowSeconds"] = "60",
        });
        var client = api.CreateApiClient();

        for (var i = 0; i < 3; i++)
            (await client.PostLoginAsync(DemoUsers.AcmeEmployee, "Wrong-Passw0rd!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var limited = await client.PostLoginAsync(DemoUsers.AcmeEmployee, "Wrong-Passw0rd!");

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await limited.ProblemCodeAsync()).Should().Be("rate_limited");
        limited.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task Session_refresh_has_its_own_looser_limit_so_page_loads_do_not_block_sign_in()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, new Dictionary<string, string>
        {
            ["RateLimiting:Auth:PermitLimit"] = "2",
            ["RateLimiting:Refresh:PermitLimit"] = "5",
        });
        var client = api.CreateApiClient();

        // Several page loads (each refreshes the session) do not use up the sign-in budget...
        for (var i = 0; i < 5; i++)
            (await client.PostRefreshAsync("not-a-token")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostRefreshAsync("not-a-token")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // ...so the user can still sign in.
        (await client.PostLoginAsync(DemoUsers.AcmeEmployee)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
