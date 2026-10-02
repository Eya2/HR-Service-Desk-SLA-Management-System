using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds demo tenants and one account per role. Runs only when the database has no tenant yet.
/// Ticket data is added in later phases.
/// </summary>
internal sealed partial class DemoDataSeeder(
    AppDbContext db,
    IPasswordHasher hasher,
    IOptions<SeedOptions> options,
    ILogger<DemoDataSeeder> logger)
{
    private sealed record DemoUser(string FirstName, string LastName, Role[] Roles);

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.Enabled)
            return;
        if (string.IsNullOrWhiteSpace(seed.DemoPassword))
        {
            LogMissingPassword(logger);
            return;
        }
        if (await db.Tenants.AnyAsync(cancellationToken))
            return;

        var passwordHash = hasher.Hash(seed.DemoPassword);

        var platform = Tenant.Create("Platform", "platform", "UTC", "en");
        db.Tenants.Add(platform);
        AddUser(platform, "platform.example", passwordHash, new DemoUser("Platform", "Admin", [Role.SuperAdmin]), managerId: null);

        AddOrganisation(
            Tenant.Create("Acme Tunisie", "acme-tn", "Africa/Tunis", "fr"), "acme.example", passwordHash,
            employee: new("Amira", "Ben Salah", [Role.Employee]),
            manager: new("Youssef", "Haddad", [Role.Employee, Role.Manager]),
            others:
            [
                new("Leila", "Mansour", [Role.Employee, Role.HrOfficer]),
                new("Sami", "Gharbi", [Role.Employee, Role.PayrollSpecialist]),
                new("Nadia", "Jaziri", [Role.Employee, Role.HrAdmin]),
                new("Hedi", "Chaabane", [Role.Auditor]),
            ]);

        AddOrganisation(
            Tenant.Create("Globex France", "globex-fr", "Europe/Paris", "fr"), "globex.example", passwordHash,
            employee: new("Camille", "Martin", [Role.Employee]),
            manager: new("Julien", "Bernard", [Role.Employee, Role.Manager]),
            others:
            [
                new("Sophie", "Laurent", [Role.Employee, Role.HrOfficer]),
                new("Thomas", "Petit", [Role.Employee, Role.PayrollSpecialist]),
                new("Claire", "Moreau", [Role.Employee, Role.HrAdmin]),
                new("Antoine", "Dubois", [Role.Auditor]),
            ]);

        await db.SaveChangesAsync(cancellationToken);
        LogSeeded(logger);
    }

    private void AddOrganisation(
        Tenant tenant, string domain, string passwordHash, DemoUser employee, DemoUser manager, DemoUser[] others)
    {
        db.Tenants.Add(tenant);
        var managerUser = AddUser(tenant, domain, passwordHash, manager, managerId: null);
        AddUser(tenant, domain, passwordHash, employee, managerUser.Id);
        foreach (var other in others)
            AddUser(tenant, domain, passwordHash, other, managerId: null);
    }

    private User AddUser(Tenant tenant, string domain, string passwordHash, DemoUser demo, Guid? managerId)
    {
        var local = $"{demo.FirstName}.{demo.LastName}".Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var user = User.Create($"{local}@{domain}", demo.FirstName, demo.LastName, demo.Roles);
        user.TenantId = tenant.Id; // no ambient tenant while seeding
        user.SetManager(managerId);
        user.SetPasswordHash(passwordHash);
        db.Users.Add(user);
        return user;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seeding is enabled but Seed:DemoPassword (SEED_PASSWORD) is empty; skipping")]
    private static partial void LogMissingPassword(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo tenants and users seeded")]
    private static partial void LogSeeded(ILogger logger);
}
