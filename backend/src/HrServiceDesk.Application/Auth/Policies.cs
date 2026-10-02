using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Application.Auth;

/// <summary>Authorization policies and the roles that satisfy each one.</summary>
public static class Policies
{
    public const string CanAdministerTenant = nameof(CanAdministerTenant);
    public const string CanWorkTickets = nameof(CanWorkTickets);
    public const string CanViewAllTickets = nameof(CanViewAllTickets);
    public const string CanApprove = nameof(CanApprove);
    public const string CanManageTeam = nameof(CanManageTeam);
    public const string CanViewDashboards = nameof(CanViewDashboards);
    public const string CanReadAudit = nameof(CanReadAudit);
    public const string CanManagePlatform = nameof(CanManagePlatform);

    public static readonly IReadOnlyDictionary<string, Role[]> RolesByPolicy = new Dictionary<string, Role[]>
    {
        [CanAdministerTenant] = [Role.HrAdmin],
        [CanWorkTickets] = [Role.HrOfficer, Role.PayrollSpecialist, Role.HrAdmin],
        [CanViewAllTickets] = [Role.HrOfficer, Role.PayrollSpecialist, Role.HrAdmin, Role.Auditor],
        [CanApprove] = [Role.Manager, Role.HrOfficer, Role.PayrollSpecialist, Role.HrAdmin],
        [CanManageTeam] = [Role.Manager],
        [CanViewDashboards] = [Role.HrAdmin, Role.Auditor, Role.HrOfficer, Role.PayrollSpecialist],
        [CanReadAudit] = [Role.HrAdmin, Role.Auditor],
        [CanManagePlatform] = [Role.SuperAdmin],
    };
}
