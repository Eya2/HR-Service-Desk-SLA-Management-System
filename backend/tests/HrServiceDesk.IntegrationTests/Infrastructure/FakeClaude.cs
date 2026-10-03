using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>Stands in for the Anthropic Messages API: records each request and answers with <see cref="Respond"/>.</summary>
public sealed class FakeClaude : HttpMessageHandler
{
    public sealed record Call(string? ApiKey, string? Version, JsonObject Body);

    public ConcurrentQueue<Call> Calls { get; } = new();

    public Func<JsonObject, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

    public static HttpResponseMessage ToolUse(string name, JsonObject input) => Json(new JsonObject
    {
        ["stop_reason"] = "tool_use",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_1", ["name"] = name, ["input"] = input }),
    });

    public static HttpResponseMessage Text(string text) => Json(new JsonObject
    {
        ["stop_reason"] = "end_turn",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
    });

    private static HttpResponseMessage Json(JsonObject body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = (await request.Content!.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        Calls.Enqueue(new Call(
            request.Headers.TryGetValues("x-api-key", out var key) ? key.Single() : null,
            request.Headers.TryGetValues("anthropic-version", out var version) ? version.Single() : null,
            body));
        return Respond(body);
    }
}

/// <summary>An API host with an Anthropic key configured and <see cref="FakeClaude"/> in place of the real API.</summary>
public sealed class AiApiFactory(string connectionString)
    : ApiFactory(connectionString, new Dictionary<string, string> { ["Ai:ApiKey"] = "test-anthropic-key", ["Ai:BaseUrl"] = "https://claude.test/" })
{
    public FakeClaude Claude { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddHttpClient("anthropic").ConfigurePrimaryHttpMessageHandler(() => Claude));
    }
}
