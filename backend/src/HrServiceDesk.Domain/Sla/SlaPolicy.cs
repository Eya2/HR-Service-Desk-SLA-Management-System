using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Domain.Sla;

/// <summary>Business-time targets for one priority.</summary>
public sealed record SlaTarget(TicketPriority Priority, int FirstResponseMinutes, int ResolutionMinutes);

/// <summary>
/// Response and resolution targets per priority, the "at risk" threshold, and the statuses during which
/// the clock pauses. A request type points to a policy; otherwise the tenant's default policy applies.
/// </summary>
public sealed class SlaPolicy : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;

    public static readonly IReadOnlySet<TicketStatus> PausableStatuses =
        new HashSet<TicketStatus> { TicketStatus.PendingApproval, TicketStatus.WaitingOnEmployee };

    private readonly List<SlaTarget> _targets = [];

    private SlaPolicy() { }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsDefault { get; private set; }

    /// <summary>Share of the target after which a case is "at risk" (percent, 50–99).</summary>
    public int AtRiskThresholdPercent { get; private set; } = 80;

    public IReadOnlyCollection<SlaTarget> Targets => _targets.AsReadOnly();

    /// <summary>Statuses that stop the clock (stored as a PostgreSQL array).</summary>
    public TicketStatus[] PauseStatuses { get; private set; } = [TicketStatus.PendingApproval, TicketStatus.WaitingOnEmployee];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static SlaPolicy Create(string name, IEnumerable<SlaTarget> targets, int atRiskThresholdPercent, IEnumerable<TicketStatus> pauseStatuses)
    {
        var policy = new SlaPolicy();
        policy.Update(name, targets, atRiskThresholdPercent, pauseStatuses);
        return policy;
    }

    public void Update(string name, IEnumerable<SlaTarget> targets, int atRiskThresholdPercent, IEnumerable<TicketStatus> pauseStatuses)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("sla.invalid_name", $"Policy name must be 1 to {NameMaxLength} characters.");
        if (atRiskThresholdPercent is < 50 or > 99)
            throw new DomainException("sla.invalid_threshold", "The at-risk threshold must be between 50 and 99 percent.");

        var list = (targets ?? []).ToList();
        if (list.Select(t => t.Priority).Distinct().Count() != Enum.GetValues<TicketPriority>().Length || list.Count != Enum.GetValues<TicketPriority>().Length)
            throw new DomainException("sla.missing_targets", "Exactly one target is required for each priority.");
        if (list.Any(t => t.FirstResponseMinutes <= 0 || t.ResolutionMinutes <= 0 || t.FirstResponseMinutes > t.ResolutionMinutes))
            throw new DomainException("sla.invalid_targets", "Targets must be positive, and first response cannot exceed resolution.");

        var pauses = (pauseStatuses ?? []).Distinct().ToArray();
        if (pauses.Any(s => !PausableStatuses.Contains(s)))
            throw new DomainException("sla.invalid_pause_status", $"The clock can only pause in: {string.Join(", ", PausableStatuses)}.");

        Name = name;
        AtRiskThresholdPercent = atRiskThresholdPercent;
        PauseStatuses = pauses;
        _targets.Clear();
        _targets.AddRange(list.OrderBy(t => t.Priority));
    }

    public void MarkAsDefault(bool isDefault) => IsDefault = isDefault;

    public SlaTarget TargetFor(TicketPriority priority) => _targets.Single(t => t.Priority == priority);
}
