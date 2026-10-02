using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>Approval chains of the demo catalog, by request type name. Types not listed need no approval.</summary>
internal static class DemoWorkflows
{
    public static readonly IReadOnlyDictionary<string, (string Name, Role Role)[]> ByRequestType =
        new Dictionary<string, (string, Role)[]>
        {
            ["Payslip correction"] = [("Manager approval", Role.Manager)],
            ["Leave request"] = [("Manager approval", Role.Manager)],
            ["Salary advance"] = [("Manager approval", Role.Manager), ("Payroll validation", Role.PayrollSpecialist)],
            ["Training request"] = [("Manager approval", Role.Manager), ("HR validation", Role.HrOfficer)],
        };
}
