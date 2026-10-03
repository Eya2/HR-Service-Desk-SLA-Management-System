using System.Text.Json.Nodes;

namespace HrServiceDesk.Application.Abstractions;

/// <summary>What a model call is for; each purpose can use a different model (fast for routing, stronger for writing).</summary>
public enum AiPurpose
{
    Classify,
    Draft,
}

/// <summary>A large language model (Claude). Unavailable when no API key is configured: callers fall back to local logic.</summary>
public interface IAiModel
{
    bool IsAvailable { get; }

    /// <summary>The model's name, shown with its suggestions.</summary>
    string Name(AiPurpose purpose);

    /// <summary>Forces a single tool call and returns its input (structured output), or null on any failure.</summary>
    Task<JsonObject?> CallToolAsync(
        AiPurpose purpose, string system, string user, string toolName, string toolDescription, JsonObject inputSchema, CancellationToken cancellationToken);

    /// <summary>Plain text completion, or null on any failure.</summary>
    Task<string?> CompleteAsync(AiPurpose purpose, string system, string user, int maxTokens, CancellationToken cancellationToken);
}
