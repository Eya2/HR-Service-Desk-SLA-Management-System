using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Integration;

public enum WebhookDeliveryStatus
{
    Pending,
    Succeeded,
    Failed,
}

/// <summary>
/// One event to send to one subscription (an outbox row written in the same transaction as the change).
/// Failed attempts are retried with growing delays; after the last one the delivery is marked failed and
/// can be sent again by hand. The id is sent as the delivery id so consumers can ignore duplicates.
/// </summary>
public sealed class WebhookDelivery : Entity, ITenantOwned
{
    public const int ErrorMaxLength = 500;

    /// <summary>Delay before attempt 2, 3, …: 1 min, 5 min, 30 min, 2 h, 6 h.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6)];

    public static int MaxAttempts => RetryDelays.Count + 1;

    private WebhookDelivery() { }

    public Guid TenantId { get; set; }

    public Guid SubscriptionId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    /// <summary>The JSON body, built once so retries send exactly the same bytes.</summary>
    public string Payload { get; private set; } = "{}";

    public Guid? TicketId { get; private set; }

    public WebhookDeliveryStatus Status { get; private set; } = WebhookDeliveryStatus.Pending;

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public int? LastStatusCode { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public static WebhookDelivery Create(Guid id, WebhookSubscription subscription, string eventType, string payload, Guid? ticketId, DateTimeOffset now) =>
        new()
        {
            Id = id,
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            EventType = eventType,
            Payload = payload,
            TicketId = ticketId,
            CreatedAt = now,
            NextAttemptAt = now,
        };

    public bool IsDue(DateTimeOffset now) => Status == WebhookDeliveryStatus.Pending && NextAttemptAt <= now;

    public void RecordSuccess(int statusCode, DateTimeOffset now)
    {
        Attempts++;
        LastAttemptAt = now;
        LastStatusCode = statusCode;
        LastError = null;
        Status = WebhookDeliveryStatus.Succeeded;
        DeliveredAt = now;
    }

    public void RecordFailure(int? statusCode, string error, DateTimeOffset now)
    {
        Attempts++;
        LastAttemptAt = now;
        LastStatusCode = statusCode;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
        if (Attempts >= MaxAttempts)
        {
            Status = WebhookDeliveryStatus.Failed;
            return;
        }

        NextAttemptAt = now + RetryDelays[Attempts - 1];
    }

    /// <summary>Sends again from the start (after the receiver has been fixed).</summary>
    public void Redeliver(DateTimeOffset now)
    {
        if (Status == WebhookDeliveryStatus.Pending)
            throw new DomainException("webhook.delivery_pending", "This delivery is already waiting to be sent.");
        Status = WebhookDeliveryStatus.Pending;
        Attempts = 0;
        NextAttemptAt = now;
        LastError = null;
    }
}
