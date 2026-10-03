using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Infrastructure.Assistant;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Anthropic API key. Empty: the assistant works offline with the local classifier and templates.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    /// <summary>Fast, inexpensive model for routing requests.</summary>
    public string ClassifyModel { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>Stronger model for writing replies.</summary>
    public string DraftModel { get; set; } = "claude-sonnet-5-5";

    public int TimeoutSeconds { get; set; } = 20;
}

/// <summary>
/// Claude through the Anthropic Messages API. Any failure (network, timeout, refusal, unexpected answer) is
/// logged and returned as null so callers fall back to the local assistant: the AI never blocks the desk.
/// </summary>
internal sealed partial class ClaudeModel(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options, ILogger<ClaudeModel> logger) : IAiModel
{
    public const string ClientName = "anthropic";
    private const string ApiVersion = "2023-06-01";

    private AiOptions Options => options.Value;

    public bool IsAvailable => !string.IsNullOrWhiteSpace(Options.ApiKey);

    public string Name(AiPurpose purpose) => purpose == AiPurpose.Classify ? Options.ClassifyModel : Options.DraftModel;

    public async Task<JsonObject?> CallToolAsync(
        AiPurpose purpose, string system, string user, string toolName, string toolDescription, JsonObject inputSchema, CancellationToken cancellationToken)
    {
        var body = Body(purpose, system, user, 1024);
        body["tools"] = new JsonArray(new JsonObject { ["name"] = toolName, ["description"] = toolDescription, ["input_schema"] = inputSchema });
        body["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = toolName };

        var response = await SendAsync(body, cancellationToken);
        return response?["content"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(block => (string?)block["type"] == "tool_use" && (string?)block["name"] == toolName)?["input"] as JsonObject;
    }

    public async Task<string?> CompleteAsync(AiPurpose purpose, string system, string user, int maxTokens, CancellationToken cancellationToken)
    {
        var response = await SendAsync(Body(purpose, system, user, maxTokens), cancellationToken);
        if (response is null)
            return null;
        if ((string?)response["stop_reason"] is "refusal")
            return null;
        var text = string.Concat(response["content"]?.AsArray().OfType<JsonObject>().Where(b => (string?)b["type"] == "text").Select(b => (string?)b["text"]) ?? []);
        return text.Length == 0 ? null : text;
    }

    private JsonObject Body(AiPurpose purpose, string system, string user, int maxTokens) => new()
    {
        ["model"] = Name(purpose),
        ["max_tokens"] = maxTokens,
        ["system"] = system,
        ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = user }),
    };

    private async Task<JsonObject?> SendAsync(JsonObject body, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
            return null;

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(Options.BaseUrl), "v1/messages"))
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Add("x-api-key", Options.ApiKey);
            request.Headers.Add("anthropic-version", ApiVersion);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogRejected(logger, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            LogUnreachable(logger, exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Anthropic API answered {Status}; using the local assistant")]
    private static partial void LogRejected(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Anthropic API could not be reached; using the local assistant")]
    private static partial void LogUnreachable(ILogger logger, Exception exception);
}
