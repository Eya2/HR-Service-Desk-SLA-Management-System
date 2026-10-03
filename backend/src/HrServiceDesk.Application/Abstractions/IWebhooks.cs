namespace HrServiceDesk.Application.Abstractions;

/// <summary>Encrypts secrets that must be read back (webhook signing secrets, SSO client secrets) at rest.</summary>
public interface ISecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}

/// <summary>One signed HTTP POST of a delivery.</summary>
public sealed record WebhookRequest(string Url, Guid DeliveryId, string EventType, string Payload, string Secret);

public sealed record WebhookResponse(bool Success, int? StatusCode, string? Error);

/// <summary>Sends webhook requests over HTTP (signing, timeout, network restrictions).</summary>
public interface IWebhookSender
{
    Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken cancellationToken);
}

/// <summary>Asks for pending deliveries to be sent soon (a background job), instead of waiting for the next sweep.</summary>
public interface IWebhookDispatchTrigger
{
    void Kick();
}

/// <summary>Deployment settings that affect validation of webhook URLs.</summary>
public interface IIntegrationSettings
{
    /// <summary>Plain-http URLs are accepted (local development and the demo network only).</summary>
    bool AllowInsecureWebhookUrls { get; }
}
