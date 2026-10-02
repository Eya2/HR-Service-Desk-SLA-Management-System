using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Integration;
using HrServiceDesk.Domain.Teams;
using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds demo data in independent, idempotent steps: tenants and one account per role (only into an
/// empty database), then the request catalog of each demo organisation that has none yet.
/// </summary>
internal sealed partial class DemoDataSeeder(
    AppDbContext db,
    IPasswordHasher hasher,
    IWebhookSecretProtector secretProtector,
    DemoHistorySeeder history,
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
        if (!await db.Tenants.AnyAsync(cancellationToken))
            await SeedTenantsAndUsersAsync(seed.DemoPassword, cancellationToken);

        await SeedCatalogAsync(cancellationToken);
        await SeedWorkflowsAsync(cancellationToken);
        await SeedTeamsAsync(cancellationToken);
        await SeedSlaAsync(cancellationToken);
        await SeedEscalationsAsync(cancellationToken);
        await SeedConfidentialityAsync(cancellationToken);
        await SeedKnowledgeAsync(cancellationToken);
        if (seed.History)
            await history.SeedAsync(seed.DemoPassword, cancellationToken);
        await SeedPayrollIntegrationAsync(seed, cancellationToken);
    }

    /// <summary>Acme's payroll connector: an API key and a webhook to the mock payroll container, when configured.</summary>
    private async Task SeedPayrollIntegrationAsync(SeedOptions seed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seed.PayrollApiKey) || string.IsNullOrWhiteSpace(seed.PayrollWebhookUrl) || string.IsNullOrWhiteSpace(seed.PayrollWebhookSecret))
            return;
        var acme = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == "acme-tn", cancellationToken);
        if (acme is null || await db.ApiKeys.IgnoreQueryFilters().AnyAsync(k => k.TenantId == acme.Id, cancellationToken))
            return;

        const string name = "Payroll connector";
        var serviceUser = await Application.Integration.ApiKeyServiceAccount.CreateAsync(db, hasher, acme.Id, name, cancellationToken);
        var key = ApiKey.FromSecret(name, ApiScopes.All, serviceUser.Id, createdById: null, seed.PayrollApiKey);
        key.TenantId = acme.Id;
        db.ApiKeys.Add(key);

        var webhook = WebhookSubscription.Create(
            "Payroll system", seed.PayrollWebhookUrl,
            [WebhookEvents.TicketStatusChanged, WebhookEvents.TicketApprovalDecided],
            secretProtector.Protect(seed.PayrollWebhookSecret), allowInsecureUrls: true);
        webhook.TenantId = acme.Id;
        db.WebhookSubscriptions.Add(webhook);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTenantsAndUsersAsync(string demoPassword, CancellationToken cancellationToken)
    {
        var passwordHash = hasher.Hash(demoPassword);

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

    private async Task SeedCatalogAsync(CancellationToken cancellationToken)
    {
        string[] demoSlugs = ["acme-tn", "globex-fr"];
        var tenants = await db.Tenants.Where(t => demoSlugs.Contains(t.Slug)).ToListAsync(cancellationToken);
        foreach (var tenant in tenants)
        {
            // No ambient tenant while seeding: the tenant filter must be bypassed and TenantId set explicitly.
            if (await db.RequestTypes.IgnoreQueryFilters().AnyAsync(t => t.TenantId == tenant.Id, cancellationToken))
                continue;

            foreach (var type in DemoCatalog.Create())
            {
                type.TenantId = tenant.Id;
                db.RequestTypes.Add(type);
            }

            LogCatalogSeeded(logger, tenant.Slug);
        }

        await db.SaveChangesAsync(cancellationToken);
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

    private async Task SeedWorkflowsAsync(CancellationToken cancellationToken)
    {
        var tenantsWithWorkflows = await db.WorkflowDefinitions.IgnoreQueryFilters().Select(w => w.TenantId).Distinct().ToListAsync(cancellationToken);
        var types = await db.RequestTypes.IgnoreQueryFilters()
            .Where(t => !tenantsWithWorkflows.Contains(t.TenantId))
            .ToListAsync(cancellationToken);

        foreach (var type in types)
        {
            if (!DemoWorkflows.ByRequestType.TryGetValue(type.Name, out var steps))
                continue;
            var workflow = WorkflowDefinition.Create(type.Id, type.IsConfidential, steps);
            workflow.TenantId = type.TenantId;
            db.WorkflowDefinitions.Add(workflow);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTeamsAsync(CancellationToken cancellationToken)
    {
        string[] demoSlugs = ["acme-tn", "globex-fr"];
        var tenantsWithTeams = await db.Teams.IgnoreQueryFilters().Select(t => t.TenantId).Distinct().ToListAsync(cancellationToken);
        var tenants = await db.Tenants.Where(t => demoSlugs.Contains(t.Slug) && !tenantsWithTeams.Contains(t.Id)).ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            var users = await db.Users.IgnoreQueryFilters().Where(u => u.TenantId == tenant.Id).ToListAsync(cancellationToken);
            var types = await db.RequestTypes.IgnoreQueryFilters().Where(t => t.TenantId == tenant.Id).ToListAsync(cancellationToken);

            foreach (var definition in DemoTeams.All)
            {
                var members = users.Where(u => u.Roles.Any(r => definition.MemberRoles.Contains(r.ToString()))).Select(u => u.Id);
                var team = Team.Create(definition.Name, definition.Strategy, members);
                team.TenantId = tenant.Id;
                db.Teams.Add(team);
                foreach (var type in types.Where(t => definition.RequestTypes.Contains(t.Name)))
                    type.SetResponsibleTeam(team.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSlaAsync(CancellationToken cancellationToken)
    {
        var configured = await db.BusinessCalendars.IgnoreQueryFilters().Select(c => c.TenantId).ToListAsync(cancellationToken);
        var tenants = await db.Tenants.Where(t => (t.Slug == "acme-tn" || t.Slug == "globex-fr") && !configured.Contains(t.Id))
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            var calendar = tenant.Slug == "acme-tn" ? DemoSla.Tunisia() : DemoSla.France();
            calendar.TenantId = tenant.Id;
            db.BusinessCalendars.Add(calendar);

            var standard = DemoSla.Standard();
            standard.TenantId = tenant.Id;
            var payroll = DemoSla.Payroll();
            payroll.TenantId = tenant.Id;
            db.SlaPolicies.AddRange(standard, payroll);

            var payrollTypes = await db.RequestTypes.IgnoreQueryFilters()
                .Where(t => t.TenantId == tenant.Id && DemoSla.PayrollRequestTypes.Contains(t.Name))
                .ToListAsync(cancellationToken);
            foreach (var type in payrollTypes)
                type.SetSlaPolicy(payroll.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedEscalationsAsync(CancellationToken cancellationToken)
    {
        var configured = await db.EscalationRules.IgnoreQueryFilters().Select(r => r.TenantId).Distinct().ToListAsync(cancellationToken);
        var tenants = await db.Tenants.Where(t => (t.Slug == "acme-tn" || t.Slug == "globex-fr") && !configured.Contains(t.Id))
            .Select(t => t.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenants)
        {
            foreach (var rule in DemoEscalations.Create())
            {
                rule.TenantId = tenantId;
                db.EscalationRules.Add(rule);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Confidential HR is the restricted group; types holding sensitive personal data are audited.</summary>
    private async Task SeedConfidentialityAsync(CancellationToken cancellationToken)
    {
        string[] sensitive = ["Change of bank details", "Leave request", "Harassment report"];
        var flagged = await db.Teams.IgnoreQueryFilters().Where(t => t.IsConfidentialGroup).Select(t => t.TenantId).ToListAsync(cancellationToken);
        foreach (var team in await db.Teams.IgnoreQueryFilters()
                     .Where(t => t.Name == "Confidential HR" && !flagged.Contains(t.TenantId)).ToListAsync(cancellationToken))
        {
            team.SetConfidentialGroup(true);
            foreach (var type in await db.RequestTypes.IgnoreQueryFilters()
                         .Where(t => t.TenantId == team.TenantId && sensitive.Contains(t.Name)).ToListAsync(cancellationToken))
                type.MarkSensitive(true);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedKnowledgeAsync(CancellationToken cancellationToken)
    {
        var configured = await db.KnowledgeArticles.IgnoreQueryFilters().Select(a => a.TenantId).Distinct().ToListAsync(cancellationToken);
        var tenants = await db.Tenants.Where(t => (t.Slug == "acme-tn" || t.Slug == "globex-fr") && !configured.Contains(t.Id))
            .Select(t => t.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenants)
        {
            foreach (var article in DemoKnowledge.Create())
            {
                article.TenantId = tenantId;
                db.KnowledgeArticles.Add(article);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo request catalog seeded for {Tenant}")]
    private static partial void LogCatalogSeeded(ILogger logger, string tenant);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo tenants and users seeded")]
    private static partial void LogSeeded(ILogger logger);
}
