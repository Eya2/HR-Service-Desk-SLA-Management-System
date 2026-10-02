using HrServiceDesk.Domain.Teams;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>Demo teams per organisation: members by role, and the request types each team handles.</summary>
internal static class DemoTeams
{
    public sealed record Definition(string Name, AssignmentStrategy Strategy, string[] MemberRoles, string[] RequestTypes);

    public static readonly Definition[] All =
    [
        new("HR Service Center", AssignmentStrategy.RoundRobin, ["HrOfficer", "HrAdmin"], ["Work certificate", "Leave request", "Training request"]),
        new("Payroll", AssignmentStrategy.LeastLoaded, ["PayrollSpecialist"], ["Payslip correction", "Salary advance", "Change of bank details"]),
        new("Confidential HR", AssignmentStrategy.Manual, ["HrAdmin"], ["Harassment report"]),
    ];
}
