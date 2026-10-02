using System.Text.Json;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Escalations;
using HrServiceDesk.Domain.Notifications;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Notifications;

/// <summary>
/// Decides who hears about each new audit event: one place for every notification rule. The person who
/// caused an event is never notified about it. Texts of confidential cases never show their title.
/// </summary>
public sealed class NotificationPlanner(IAppDbContext db)
{
    public async Task<List<Notification>> PlanAsync(IReadOnlyList<TicketEvent> events, IReadOnlyDictionary<Guid, Ticket> tickets, CancellationToken cancellationToken)
    {
        var result = new List<Notification>();
        foreach (var e in events)
        {
            if (!tickets.TryGetValue(e.TicketId, out var ticket))
                continue;

            var tenantId = ticket.TenantId != Guid.Empty ? ticket.TenantId : e.TenantId;
            var data = JsonDocument.Parse(e.Data).RootElement;
            var subject = ticket.IsConfidential ? $"{ticket.Reference} (confidential case)" : $"{ticket.Reference} · {ticket.Title}";

            var (type, recipients, title, message) = e.Type switch
            {
                TicketEventType.Created => (NotificationType.TicketCreated, One(ticket.RequesterId), $"{ticket.Reference} received",
                    $"Your request {subject} was submitted."),
                TicketEventType.Assigned when IdOf(data, "to") is { } assignee => (NotificationType.TicketAssigned, One(assignee),
                    $"{ticket.Reference} assigned to you", $"You now handle {subject}."),
                TicketEventType.StatusChanged => (NotificationType.StatusChanged, One(ticket.RequesterId),
                    $"{ticket.Reference}: {Text(data, "to")}", $"The status of {subject} changed from {Text(data, "from")} to {Text(data, "to")}."),
                TicketEventType.ApprovalRequested => (NotificationType.ApprovalRequested, await ApproversAsync(ticket, tenantId, cancellationToken),
                    $"{ticket.Reference} needs your approval", $"Please review {subject} ({Text(data, "step")})."),
                TicketEventType.CommentAdded => (NotificationType.CommentAdded, CommentRecipients(ticket, e, data),
                    $"New message on {ticket.Reference}", $"There is a new message on {subject}."),
                TicketEventType.SlaStateChanged when Text(data, "to") is "AtRisk" or "Breached" => (
                    Text(data, "to") == "Breached" ? NotificationType.SlaBreached : NotificationType.SlaAtRisk,
                    await HandlersAsync(ticket, tenantId, cancellationToken),
                    $"{ticket.Reference}: SLA {(Text(data, "to") == "Breached" ? "breached" : "at risk")}",
                    $"{subject} is {(Text(data, "to") == "Breached" ? "past its deadline" : "close to its deadline")}."),
                TicketEventType.Escalated => (NotificationType.Escalated, await EscalationRecipientsAsync(ticket, tenantId, data, cancellationToken),
                    $"{ticket.Reference} escalated", $"{subject} was escalated by the rule \"{Text(data, "rule")}\"."),
                _ => (NotificationType.StatusChanged, [], string.Empty, string.Empty),
            };

            // Nobody is told about what they did themselves, except the confirmation of their own submission.
            foreach (var userId in recipients.Distinct().Where(u => u != e.ActorId || e.Type == TicketEventType.Created))
                result.Add(new Notification(tenantId, userId, type, title, message, ticket.Id, e.OccurredAt));
        }

        return result;
    }

    private static List<Guid> One(Guid userId) => [userId];

    private static string Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : string.Empty;

    private static Guid? IdOf(JsonElement data, string name) =>
        data.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var id) ? id : null;

    /// <summary>Public replies go to the other side of the conversation; internal notes to the assignee only.</summary>
    private static List<Guid> CommentRecipients(Ticket ticket, TicketEvent e, JsonElement data)
    {
        var isInternal = data.TryGetProperty("isInternal", out var flag) && flag.ValueKind == JsonValueKind.True;
        if (isInternal || e.ActorId == ticket.RequesterId)
            return ticket.AssigneeId is { } assignee ? [assignee] : [];
        return [ticket.RequesterId];
    }

    /// <summary>The named approver, or everyone holding the step's role (HR Admins only for confidential cases).</summary>
    private async Task<List<Guid>> ApproversAsync(Ticket ticket, Guid tenantId, CancellationToken cancellationToken)
    {
        if (ticket.CurrentApproval is not { } step)
            return [];
        if (step.ApproverUserId is { } named)
            return [named];

        var role = ticket.IsConfidential ? Role.HrAdmin : step.ApproverRole;
        return await UsersWithRoleAsync(tenantId, role, cancellationToken);
    }

    /// <summary>The assignee, or the members of the case's team when nobody is assigned.</summary>
    private async Task<List<Guid>> HandlersAsync(Ticket ticket, Guid tenantId, CancellationToken cancellationToken)
    {
        if (ticket.AssigneeId is { } assignee)
            return [assignee];
        if (ticket.TeamId is not { } teamId)
            return await UsersWithRoleAsync(tenantId, Role.HrAdmin, cancellationToken);

        var team = await db.Teams.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        return team?.OrderedMemberIds.ToList() ?? [];
    }

    private async Task<List<Guid>> EscalationRecipientsAsync(Ticket ticket, Guid tenantId, JsonElement data, CancellationToken cancellationToken)
    {
        var action = Text(data, "action");
        if (action == nameof(EscalationAction.NotifyManager))
        {
            var managerId = ticket.AssigneeId is { } assignee
                ? await db.Users.IgnoreQueryFilters().Where(u => u.Id == assignee).Select(u => u.ManagerId).SingleOrDefaultAsync(cancellationToken)
                : null;
            return managerId is { } manager ? [manager] : await UsersWithRoleAsync(tenantId, Role.HrAdmin, cancellationToken);
        }

        // Other actions (notify, raise priority, reassign) are reported to whoever handles the case now.
        return await HandlersAsync(ticket, tenantId, cancellationToken);
    }

    private Task<List<Guid>> UsersWithRoleAsync(Guid tenantId, Role role, CancellationToken cancellationToken) =>
        db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Roles.Contains(role))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
}
