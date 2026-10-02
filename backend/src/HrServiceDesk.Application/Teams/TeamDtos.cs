namespace HrServiceDesk.Application.Teams;

public sealed record TeamMemberDto(Guid Id, string FullName, int ActiveCases);

public sealed record TeamDto(
    Guid Id, string Name, string Strategy, bool IsConfidentialGroup, IReadOnlyList<TeamMemberDto> Members, IReadOnlyList<string> RequestTypes);
