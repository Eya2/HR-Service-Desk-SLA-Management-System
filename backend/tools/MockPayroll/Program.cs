using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MockPayroll;

// A stand-in for an external payroll system, used by the demo. It receives the desk's webhooks (checking
// their signature), and when a payroll case becomes ready to work on it "processes" it through the
// integration API: it takes the case, then resolves it with a payroll reference.
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<PayrollSettings>(builder.Configuration.GetSection("Payroll"));
builder.Services.AddSingleton<EventLog>();
builder.Services.AddSingleton<PayrollProcessor>();
builder.Services.AddHttpClient(PayrollProcessor.ClientName, (sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayrollSettings>>().Value;
    client.BaseAddress = new Uri(settings.DeskApiUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
});

var app = builder.Build();

app.MapPost("/webhooks", async (HttpRequest request, EventLog log, PayrollProcessor processor, Microsoft.Extensions.Options.IOptions<PayrollSettings> options) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();
    var deliveryId = request.Headers["X-HrDesk-Delivery"].ToString();
    var eventType = request.Headers["X-HrDesk-Event"].ToString();

    if (!Signature.Verify(options.Value.WebhookSecret, request.Headers["X-HrDesk-Signature"], body, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)))
    {
        log.Add(new ReceivedEvent(DateTimeOffset.UtcNow, deliveryId, eventType, null, "Rejected: bad signature"));
        return Results.Unauthorized();
    }

    // Deliveries can be retried: the delivery id makes processing idempotent.
    if (!log.FirstTime(deliveryId))
        return Results.Ok(new { duplicate = true });

    var payload = JsonNode.Parse(body);
    var ticket = payload?["data"]?["ticket"];
    var reference = ticket?["reference"]?.GetValue<string>();
    var action = processor.Consider(eventType, payload);
    log.Add(new ReceivedEvent(DateTimeOffset.UtcNow, deliveryId, eventType, reference, action));
    return Results.Accepted();
});

app.MapGet("/events", (EventLog log) => log.All());
app.MapGet("/health", () => Results.Ok("ok"));
app.MapGet("/", (EventLog log) => Results.Content(Page.Render(log.All()), "text/html; charset=utf-8"));

await app.RunAsync();

namespace MockPayroll
{
    internal sealed class PayrollSettings
    {
        public string DeskApiUrl { get; set; } = "http://api:8080";

        public string ApiKey { get; set; } = string.Empty;

        public string WebhookSecret { get; set; } = string.Empty;

        public int ProcessingDelaySeconds { get; set; } = 5;
    }

    internal sealed record ReceivedEvent(DateTimeOffset ReceivedAt, string DeliveryId, string EventType, string? Reference, string Outcome);

    internal sealed class EventLog
    {
        private readonly ConcurrentQueue<ReceivedEvent> _events = new();
        private readonly ConcurrentDictionary<string, bool> _seen = new();

        public bool FirstTime(string deliveryId) => string.IsNullOrEmpty(deliveryId) || _seen.TryAdd(deliveryId, true);

        public void Add(ReceivedEvent e)
        {
            _events.Enqueue(e);
            while (_events.Count > 200 && _events.TryDequeue(out _))
            {
            }
        }

        public IReadOnlyList<ReceivedEvent> All() => _events.Reverse().ToList();
    }

    /// <summary>Same scheme as the desk: t=&lt;unix&gt;,v1=&lt;hex HMAC-SHA256(secret, "t.body")&gt;.</summary>
    internal static class Signature
    {
        public static bool Verify(string secret, string? header, string body, DateTimeOffset now, TimeSpan tolerance)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header))
                return false;
            var parts = header.Split(',').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            if (!parts.TryGetValue("t", out var t) || !long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
                || !parts.TryGetValue("v1", out var received))
                return false;
            if ((now - DateTimeOffset.FromUnixTimeSeconds(timestamp)).Duration() > tolerance)
                return false;
            var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{t}.{body}"));
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Convert.ToHexString(mac).ToLowerInvariant()), Encoding.ASCII.GetBytes(received));
        }
    }

    /// <summary>Works payroll cases that just became ready (status Open), in the background.</summary>
    internal sealed partial class PayrollProcessor(
        IHttpClientFactory httpClients,
        Microsoft.Extensions.Options.IOptions<PayrollSettings> options,
        EventLog log,
        ILogger<PayrollProcessor> logger)
    {
        public const string ClientName = "desk";
        private int _sequence;

        public string Consider(string eventType, JsonNode? payload)
        {
            var ticket = payload?["data"]?["ticket"];
            var category = ticket?["requestType"]?["category"]?.GetValue<string>();
            var to = payload?["data"]?["change"]?["to"]?.GetValue<string>();
            if (eventType != "ticket.status_changed" || to != "Open" || category != "Payroll" || ticket?["id"]?.GetValue<Guid>() is not { } id)
                return "Ignored";

            _ = Task.Run(() => ProcessAsync(id));
            return "Processing";
        }

        private async Task ProcessAsync(Guid id)
        {
            var delay = TimeSpan.FromSeconds(options.Value.ProcessingDelaySeconds);
            var client = httpClients.CreateClient(ClientName);
            try
            {
                await Task.Delay(delay);
                var ticket = await client.GetFromJsonAsync<JsonNode>($"/api/integration/v1/tickets/{id}");
                var reference = ticket?["reference"]?.GetValue<string>() ?? id.ToString();
                var type = ticket?["requestType"]?["name"]?.GetValue<string>() ?? "Payroll request";

                await PostAsync(client, $"/api/integration/v1/tickets/{id}/status", new { status = "InProgress" });
                await Task.Delay(delay);

                var payrollReference = $"PAY-{DateTime.UtcNow:yyyyMM}-{Interlocked.Increment(ref _sequence):D4}";
                var message = type switch
                {
                    "Payslip correction" => $"Payroll system: the correction is booked as {payrollReference} and will be paid with the next pay run.",
                    "Change of bank details" => $"Payroll system: the new bank account is registered ({payrollReference}) and applies from the next pay run.",
                    _ => $"Payroll system: processed as {payrollReference}.",
                };
                await PostAsync(client, $"/api/integration/v1/tickets/{id}/status", new { status = "Resolved", reason = message });
                log.Add(new ReceivedEvent(DateTimeOffset.UtcNow, string.Empty, "payroll.processed", reference, $"Resolved as {payrollReference}"));
            }
#pragma warning disable CA1031 // A demo service logs and moves on.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogFailed(logger, ex, id);
                log.Add(new ReceivedEvent(DateTimeOffset.UtcNow, string.Empty, "payroll.failed", id.ToString(), ex.Message));
            }
        }

        private static async Task PostAsync(HttpClient client, string path, object body)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.UnprocessableEntity)
                response.EnsureSuccessStatusCode();
        }

        [LoggerMessage(Level = LogLevel.Error, Message = "Processing case {TicketId} failed")]
        private static partial void LogFailed(ILogger logger, Exception exception, Guid ticketId);
    }

    internal static class Page
    {
        public static string Render(IReadOnlyList<ReceivedEvent> events)
        {
            var rows = new StringBuilder();
            foreach (var e in events)
            {
                rows.Append(CultureInfo.InvariantCulture, $"<tr><td>{e.ReceivedAt:HH:mm:ss}</td><td>{WebUtility.HtmlEncode(e.EventType)}</td>")
                    .Append(CultureInfo.InvariantCulture, $"<td>{WebUtility.HtmlEncode(e.Reference ?? "")}</td><td>{WebUtility.HtmlEncode(e.Outcome)}</td></tr>");
            }

            return $$"""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><meta http-equiv="refresh" content="3">
                <title>Mock payroll</title>
                <style>body{font:14px system-ui;margin:32px;color:#1f2937}table{border-collapse:collapse;width:100%}
                td,th{padding:6px 10px;border-bottom:1px solid #e5e7eb;text-align:left}th{color:#6b7280;font-weight:600}</style></head>
                <body><h1>Mock payroll system</h1><p>Webhooks received from HR Service Desk (newest first, refreshes every 3 s).</p>
                <table><tr><th>Time (UTC)</th><th>Event</th><th>Case</th><th>Outcome</th></tr>{{rows}}</table></body></html>
                """;
        }
    }
}
