namespace HrServiceDesk.Domain.Users;

/// <summary>Business roles. A user can hold several (a Manager is usually also an Employee).</summary>
public enum Role
{
    Employee,
    Manager,
    HrOfficer,
    PayrollSpecialist,
    HrAdmin,
    Auditor,
    SuperAdmin,
}
