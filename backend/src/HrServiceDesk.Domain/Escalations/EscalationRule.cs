using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Sla;

namespace HrServiceDesk.Domain.Escalations;

public enum EscalationTrigger
{
    /// <summary>The case's SLA is at risk (or worse).</summary>
    AtRisk,

    /// <summary>The case's SLA is breached.</summary>
    Breached,

    /// <summary>HR has not responded for <see cref="EscalationRule.NoResponseHours"/> business hours.</summary>
    NoResponseFor,
}

public enum EscalationAction
{
    NotifyAssignee,

    /// <summary>The assignee's manager (HR Admins when the assignee has none, or nobody is assigned).</summary>
    NotifyManager,

    BumpPriority,

    ReassignToTeam,
}

/// <summary>What the monitor knows about a case when evaluating rules.</summary>
public sealed record EscalationFacts(Guid RequestTypeId, SlaState SlaState, int? BusinessMinutesWithoutResponse, bool IsFinal);

/// <summary>"When &lt;trigger&gt;, do &lt;action&gt;", optionally for one request type. Fires at most once per case.</summary>
public sealed class EscalationRule : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;

    private EscalationRule() { }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    public EscalationTrigger Trigger { get; private set; }

    public int? NoResponseHours { get; private set; }

    public EscalationAction Action { get; private set; }

    public Guid? TargetTeamId { get; private set; }

    /// <summary>Limits the rule to one request type; null applies it to all.</summary>
    public Guid? RequestTypeId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static EscalationRule Create(
        string name, EscalationTrigger trigger, int? noResponseHours, EscalationAction action, Guid? targetTeamId, Guid? requestTypeId)
    {
        var rule = new EscalationRule();
        rule.Update(name, trigger, noResponseHours, action, targetTeamId, requestTypeId, isActive: true);
        return rule;
    }

    public void Update(
        string name, EscalationTrigger trigger, int? noResponseHours, EscalationAction action, Guid? targetTeamId, Guid? requestTypeId, bool isActive)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("escalation.invalid_name", $"Rule name must be 1 to {NameMaxLength} characters.");
        if (trigger == EscalationTrigger.NoResponseFor && noResponseHours is not (> 0 and <= 720))
            throw new DomainException("escalation.invalid_hours", "\"No response for\" needs 1 to 720 business hours.");
        if (action == EscalationAction.ReassignToTeam && targetTeamId is null)
            throw new DomainException("escalation.missing_team", "Reassigning needs a target team.");

        Name = name;
        Trigger = trigger;
        NoResponseHours = trigger == EscalationTrigger.NoResponseFor ? noResponseHours : null;
        Action = action;
        TargetTeamId = action == EscalationAction.ReassignToTeam ? targetTeamId : null;
        RequestTypeId = requestTypeId;
        IsActive = isActive;
    }

    /// <summary>Whether the rule applies to a case now (it still has to not have fired before).</summary>
    public bool IsTriggeredBy(EscalationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!IsActive || facts.IsFinal || (RequestTypeId is { } typeId && typeId != facts.RequestTypeId))
            return false;

        return Trigger switch
        {
            EscalationTrigger.AtRisk => facts.SlaState >= SlaState.AtRisk,
            EscalationTrigger.Breached => facts.SlaState == SlaState.Breached,
            EscalationTrigger.NoResponseFor => facts.BusinessMinutesWithoutResponse >= NoResponseHours * 60,
            _ => false,
        };
    }
}

/// <summary>Proof that a rule fired for a case; unique per (case, rule), which makes rules idempotent.</summary>
public sealed class EscalationExecution : Entity, ITenantOwned
{
    private EscalationExecution() { }

    public EscalationExecution(Guid tenantId, Guid ticketId, Guid ruleId, DateTimeOffset executedAt)
    {
        TenantId = tenantId;
        TicketId = ticketId;
        RuleId = ruleId;
        ExecutedAt = executedAt;
    }

    public Guid TenantId { get; set; }

    public Guid TicketId { get; private set; }

    public Guid RuleId { get; private set; }

    public DateTimeOffset ExecutedAt { get; private set; }
}
