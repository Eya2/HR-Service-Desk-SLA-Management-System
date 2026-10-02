namespace HrServiceDesk.Application.Dashboard;

public sealed record NamedCount(string Key, string Label, int Count);

public sealed record ComplianceRow(string Key, string Label, int Resolved, int Met, double? CompliancePercent);

public sealed record VolumePoint(DateOnly Date, int Created, int Resolved);

public sealed record AgentWorkload(Guid UserId, string Name, int ActiveCases, int ResolvedInPeriod);

public sealed record TeamOption(Guid Id, string Name);

public sealed record DashboardKpis(
    int Created,
    int Resolved,
    int Backlog,
    int AtRiskNow,
    int BreachedNow,
    double? SlaCompliancePercent,
    double? AverageFirstResponseHours,
    double? AverageResolutionHours,
    double? ReopenRatePercent,
    double? AverageSatisfaction,
    int Ratings);

/// <summary>HR leadership dashboard for a period (and optionally one team). Hours are business hours, pauses excluded.</summary>
public sealed record DashboardDto(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? TeamId,
    string Granularity,
    DashboardKpis Kpis,
    IReadOnlyList<ComplianceRow> ComplianceByRequestType,
    IReadOnlyList<ComplianceRow> ComplianceByTeam,
    IReadOnlyList<NamedCount> BacklogByStatus,
    IReadOnlyList<NamedCount> BacklogByPriority,
    IReadOnlyList<NamedCount> TopCategories,
    IReadOnlyList<VolumePoint> Volume,
    IReadOnlyList<AgentWorkload> Workload,
    IReadOnlyList<TeamOption> Teams);
