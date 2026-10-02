namespace HrServiceDesk.Application.MyTeam;

public sealed record StatusCountDto(string Status, int Count);

/// <summary>Activity of the manager's direct reports (confidential cases excluded).</summary>
public sealed record TeamStatsDto(
    int TeamSize,
    int OpenCases,
    int SubmittedLast30Days,
    int PendingMyApproval,
    IReadOnlyList<StatusCountDto> ByStatus);
