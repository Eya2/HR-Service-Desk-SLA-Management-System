using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Integration;

/// <summary>Event types a subscription can receive. Payloads are thin: consumers fetch details with an API key.</summary>
public static class WebhookEvents
{
    public const string TicketCreated = "ticket.created";
    public const string TicketStatusChanged = "ticket.status_changed";
    public const string TicketApprovalDecided = "ticket.approval_decided";
    public const string TicketCommentAdded = "ticket.comment_added";
    public const string TicketSlaBreached = "ticket.sla_breached";

    /// <summary>Sent on demand from the admin screen to check the endpoint.</summary>
    public const string Ping = "ping";

    public static readonly IReadOnlyList<string> All = [TicketCreated, TicketStatusChanged, TicketApprovalDecided, TicketCommentAdded, TicketSlaBreached];
}

/// <summary>
/// An HTTP endpoint that receives signed event notifications. The signing secret is stored encrypted
/// (see the infrastructure's secret protector) and shown once when created or rotated.
/// </summary>
public sealed class WebhookSubscription : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;
    public const int UrlMaxLength = 500;

    private WebhookSubscription() { }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    /// <summary>The signing secret, encrypted.</summary>
    public string ProtectedSecret { get; private set; } = string.Empty;

    public string[] Events { get; private set; } = [];

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static WebhookSubscription Create(string name, string url, IEnumerable<string> events, string protectedSecret, bool allowInsecureUrls)
    {
        var subscription = new WebhookSubscription { ProtectedSecret = protectedSecret };
        subscription.Update(name, url, events, isActive: true, allowInsecureUrls);
        return subscription;
    }

    public void Update(string name, string url, IEnumerable<string> events, bool isActive, bool allowInsecureUrls)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("webhook.invalid_name", $"Names must be 1 to {NameMaxLength} characters.");
        Url = ValidateUrl(url, allowInsecureUrls);
        var list = (events ?? []).Distinct().ToArray();
        if (list.Length == 0 || list.Any(e => !WebhookEvents.All.Contains(e)))
            throw new DomainException("webhook.invalid_events", $"Events must be among: {string.Join(", ", WebhookEvents.All)}.");
        Name = name;
        Events = list;
        IsActive = isActive;
    }

    public void RotateSecret(string protectedSecret) => ProtectedSecret = protectedSecret;

    public bool Wants(string eventType) => IsActive && Events.Contains(eventType);

    /// <summary>Absolute HTTPS URL without credentials; plain HTTP only where allowed (local development, the demo network).</summary>
    public static string ValidateUrl(string url, bool allowInsecure)
    {
        url = (url ?? string.Empty).Trim();
        if (url.Length is 0 or > UrlMaxLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(allowInsecure && uri.Scheme == Uri.UriSchemeHttp))
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new DomainException("webhook.invalid_url", allowInsecure
                ? "The URL must be an absolute http(s) address without credentials."
                : "The URL must be an absolute https address without credentials.");
        }

        return uri.ToString();
    }
}
