namespace HrServiceDesk.Application.Sla;

public sealed record WorkingIntervalDto(string Day, string Start, string End);

public sealed record HolidayDto(DateOnly Date, string Name);

public sealed record CalendarDto(Guid Id, string Name, string TimeZoneId, IReadOnlyList<WorkingIntervalDto> WorkingHours, IReadOnlyList<HolidayDto> Holidays);

public sealed record SlaTargetDto(string Priority, int FirstResponseMinutes, int ResolutionMinutes);

public sealed record SlaPolicyDto(
    Guid Id, string Name, bool IsDefault, int AtRiskThresholdPercent, IReadOnlyList<SlaTargetDto> Targets,
    IReadOnlyList<string> PauseStatuses, IReadOnlyList<string> RequestTypes);

/// <summary>SLA position of a case. Due dates are null while the clock is paused.</summary>
public sealed record TicketSlaDto(
    string State,
    bool IsPaused,
    DateTimeOffset? FirstResponseDueAt,
    DateTimeOffset? ResolutionDueAt,
    DateTimeOffset? FirstRespondedAt,
    bool FirstResponseBreached,
    bool ResolutionBreached,
    int? FirstResponseTargetMinutes,
    int? ResolutionTargetMinutes);
