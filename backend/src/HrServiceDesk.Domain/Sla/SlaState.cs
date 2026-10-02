namespace HrServiceDesk.Domain.Sla;

/// <summary>Where a case stands against its SLA. Ordered from best to worst.</summary>
public enum SlaState
{
    /// <summary>No SLA applies (no policy or no calendar).</summary>
    None,
    OnTrack,

    /// <summary>The at-risk share of the target (80% by default) is used up.</summary>
    AtRisk,
    Breached,
}

/// <summary>A period during which the SLA clock did not run (<see cref="To"/> null while it still does not).</summary>
public sealed record SlaPause(DateTimeOffset From, DateTimeOffset? To);
