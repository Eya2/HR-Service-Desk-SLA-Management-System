using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class HostTests(PostgresFixture postgres)
{
    private readonly HttpClient _client = postgres.Api.CreateApiClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy_without_authentication(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task System_info_is_public_and_returns_service_metadata()
    {
        var info = await _client.GetFromJsonAsync<SystemInfo>("/api/system/info");

        info!.Name.Should().Be("HR Service Desk API");
        info.Environment.Should().Be("Testing");
    }

    [Fact]
    public async Task Unknown_route_is_401_for_anonymous_callers_and_404_once_signed_in()
    {
        var anonymous = await _client.GetAsync("/api/does-not-exist");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        anonymous.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var client = postgres.Api.CreateApiClient();
        client.Authorize(await client.LoginAsync(DemoUsers.AcmeEmployee));
        var signedIn = await client.GetAsync("/api/does-not-exist");
        signedIn.StatusCode.Should().Be(HttpStatusCode.NotFound);
        signedIn.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Correlation_id_is_echoed_when_valid()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "abc-123");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be("abc-123");
    }

    [Fact]
    public async Task Correlation_id_is_replaced_when_unsafe()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "bad value\r\ninjected");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task Swagger_document_is_served_with_bearer_scheme()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("/api/auth/login").And.Contain("\"bearer\"");
    }

    private sealed record SystemInfo(string Name, string Version, string Environment);
}
