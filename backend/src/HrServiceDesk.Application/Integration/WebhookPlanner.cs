using System.Text.Json;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Integration;
using HrServiceDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Integration;

/// <summary>
/// Turns new case events into webhook deliveries for the organisation's subscriptions (outbox rows saved with
/// the change). Payloads are thin — identifiers, statuses and the request type — and confidential cases are
/// never sent. Internal notes are not sent either.
/// </summary>
public sealed class WebhookPlanner(IAppDbContext db, ITenantContext tenantContext, TimeProvider clock)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<List<WebhookDelivery>> PlanAsync(IReadOnlyList<TicketEvent> events, IReadOnlyDictionary<Guid, Ticket> tickets, CancellationToken cancellationToken)
    {
        var candidates = new List<(TicketEvent Event, Ticket Ticket, string Type, Guid TenantId)>();
        foreach (var e in events)
        {
            if (!tickets.TryGetValue(e.TicketId, out var ticket) || ticket.IsConfidential || TypeOf(e) is not { } type)
                continue;
            var tenantId = ticket.TenantId != Guid.Empty ? ticket.TenantId : e.TenantId != Guid.Empty ? e.TenantId : tenantContext.TenantId ?? Guid.Empty;
            if (tenantId != Guid.Empty)
                candidates.Add((e, ticket, type, tenantId));
        }

        if (candidates.Count == 0)
            return [];

        var tenantIds = candidates.Select(c => c.TenantId).Distinct().ToList();
        var subscriptions = await db.WebhookSubscriptions.IgnoreQueryFilters()
            .Where(s => tenantIds.Contains(s.TenantId) && s.IsActive)
            .ToListAsync(cancellationToken);
        if (subscriptions.Count == 0)
            return [];

        var typeIds = candidates.Select(c => c.Ticket.RequestTypeId).Distinct().ToList();
        var requestTypes = await db.RequestTypes.IgnoreQueryFilters().AsNoTracking()
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => new { t.Id, t.Name, Category = t.Category.ToString() }, cancellationToken);

        var now = clock.GetUtcNow();
        var deliveries = new List<WebhookDelivery>();
        foreach (var (e, ticket, type, tenantId) in candidates)
        {
            foreach (var subscription in subscriptions.Where(s => s.TenantId == tenantId && s.Wants(type)))
            {
                var id = Guid.NewGuid();
                var payload = JsonSerializer.Serialize(
                    new
                    {
                        id,
                        type,
                        occurredAt = e.OccurredAt,
                        data = new
                        {
                            ticket = new
                            {
                                id = ticket.Id,
                                reference = ticket.Reference,
                                status = ticket.Status.ToString(),
                                priority = ticket.Priority.ToString(),
                                requestType = requestTypes.GetValueOrDefault(ticket.RequestTypeId),
                                requesterId = ticket.RequesterId,
                                teamId = ticket.TeamId,
                                assigneeId = ticket.AssigneeId,
                            },
                            change = Change(e.Data),
                        },
                    },
                    Json);
                deliveries.Add(WebhookDelivery.Create(id, subscription, type, payload, ticket.Id, now));
            }
        }

        return deliveries;
    }

    internal static string? TypeOf(TicketEvent e) => e.Type switch
    {
        TicketEventType.Created => WebhookEvents.TicketCreated,
        TicketEventType.StatusChanged => WebhookEvents.TicketStatusChanged,
        TicketEventType.ApprovalDecided => WebhookEvents.TicketApprovalDecided,
        TicketEventType.CommentAdded when !e.IsInternal && !Flag(e.Data, "isInternal") => WebhookEvents.TicketCommentAdded,
        TicketEventType.SlaStateChanged when Text(e.Data, "to") == "Breached" => WebhookEvents.TicketSlaBreached,
        _ => null,
    };

    /// <summary>Only facts travel (statuses, decision, step name); free text such as reasons and comments stays in the desk.</summary>
    private static Dictionary<string, JsonElement> Change(string data)
    {
        using var document = JsonDocument.Parse(data);
        return document.RootElement.EnumerateObject()
            .Where(p => p.Name is "from" or "to" or "decision" or "step")
            .ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static bool Flag(string data, string name)
    {
        using var document = JsonDocument.Parse(data);
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static string? Text(string data, string name)
    {
        using var document = JsonDocument.Parse(data);
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
