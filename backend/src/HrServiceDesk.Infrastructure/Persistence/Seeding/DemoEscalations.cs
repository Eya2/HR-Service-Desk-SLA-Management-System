using HrServiceDesk.Domain.Escalations;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>Escalation rules of the demo organisations.</summary>
internal static class DemoEscalations
{
    public static IEnumerable<EscalationRule> Create() =>
    [
        EscalationRule.Create("Warn the assignee when at risk", EscalationTrigger.AtRisk, null, EscalationAction.NotifyAssignee, null, null),
        EscalationRule.Create("Alert the manager on breach", EscalationTrigger.Breached, null, EscalationAction.NotifyManager, null, null),
        EscalationRule.Create("Raise the priority on breach", EscalationTrigger.Breached, null, EscalationAction.BumpPriority, null, null),
        EscalationRule.Create("Chase unanswered cases after 4 business hours", EscalationTrigger.NoResponseFor, 4, EscalationAction.NotifyAssignee, null, null),
    ];
}
