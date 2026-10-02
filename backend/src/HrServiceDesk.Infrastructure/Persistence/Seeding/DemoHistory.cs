using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Knowledge;
using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Teams;
using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>
/// Six weeks of believable activity per demo organisation, so dashboards, queues and SLAs have something to show:
/// more employees, cases in every status, approvals, replies, late cases, ratings and help-article statistics.
/// Runs only for an organisation without any case. The cases are built with the domain model at past
/// timestamps and saved through a context without interceptors, so no notification, e-mail or webhook fires
/// for history. Open cases keep the SLA state of their last step: the live SLA monitor then detects the
/// at-risk and late ones and escalates them as it would in real life.
/// </summary>
internal sealed partial class DemoHistorySeeder(
    AppDbContext db,
    IPasswordHasher hasher,
    IFileStorage storage,
    TimeProvider clock,
    ILogger<DemoHistorySeeder> logger)
{
    private static readonly (string Slug, int Seed, int MaxPerDay, string Domain, (string First, string Last)[] Employees)[] Organisations =
    [
        ("acme-tn", 2026, 3, "acme.example",
            [("Mehdi", "Trabelsi"), ("Ines", "Khelifi"), ("Karim", "Bouazizi"), ("Salma", "Ayari"), ("Walid", "Mejri"), ("Rania", "Hammami")]),
        ("globex-fr", 1789, 2, "globex.example",
            [("Lucas", "Girard"), ("Emma", "Roux"), ("Hugo", "Lefebvre"), ("Chloe", "Fontaine"), ("Nathan", "Mercier"), ("Lea", "Garnier")]),
    ];

    public async Task SeedAsync(string demoPassword, CancellationToken cancellationToken)
    {
        foreach (var organisation in Organisations)
        {
            var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == organisation.Slug, cancellationToken);
            if (tenant is null || await db.Tickets.IgnoreQueryFilters().AnyAsync(t => t.TenantId == tenant.Id, cancellationToken))
                continue;

            await using var raw = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()).UseSnakeCaseNamingConvention().Options,
                new NoTenant());
            var generator = new Generator(raw, tenant, hasher.Hash(demoPassword), storage, clock.GetUtcNow(), organisation.Seed);
            var count = await generator.RunAsync(organisation.Domain, organisation.Employees, organisation.MaxPerDay, cancellationToken);
            LogSeeded(logger, count, tenant.Slug);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo history: {Count} cases seeded for {Tenant}")]
    private static partial void LogSeeded(ILogger logger, int count, string tenant);

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }

    private sealed class Generator(AppDbContext raw, Tenant tenant, string passwordHash, IFileStorage storage, DateTimeOffset now, int seed)
    {
#pragma warning disable CA5394 // Demo data: reproducible pseudo-randomness, no security use.
        private readonly Random _random = new(seed);
#pragma warning restore CA5394
        private readonly List<SatisfactionRating> _ratings = [];
        private Dictionary<string, RequestType> _types = [];
        private Dictionary<Guid, WorkflowDefinition> _workflows = [];
        private Dictionary<Guid, Team> _teams = [];
        private List<SlaPolicy> _policies = [];
        private List<User> _users = [];
        private List<User> _requesters = [];
        private BusinessTimeCalculator _calendar = null!;
        private TimeZoneInfo _zone = TimeZoneInfo.Utc;
        private readonly Dictionary<Guid, int> _roundRobin = [];

        private Guid TenantId => tenant.Id;

        public async Task<int> RunAsync(string domain, (string First, string Last)[] employees, int maxPerDay, CancellationToken cancellationToken)
        {
            await LoadAsync(domain, employees, cancellationToken);

            var plans = Plan(maxPerDay);
            foreach (var plan in plans.OrderBy(p => p.CreatedAt))
                await CreateAsync(plan, cancellationToken);

            await HotCasesAsync(cancellationToken);
            await KnowledgeStatisticsAsync(cancellationToken);
            raw.SatisfactionRatings.AddRange(_ratings);
            await raw.SaveChangesAsync(cancellationToken);
            return plans.Count + 2;
        }

        private sealed record CasePlan(DateTimeOffset CreatedAt, string Type, User Requester);

        private async Task LoadAsync(string domain, (string First, string Last)[] employees, CancellationToken cancellationToken)
        {
            _zone = TimeZoneInfo.TryFindSystemTimeZoneById(tenant.TimeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
            _types = await raw.RequestTypes.IgnoreQueryFilters().Where(t => t.TenantId == TenantId).ToDictionaryAsync(t => t.Name, cancellationToken);
            _workflows = await raw.WorkflowDefinitions.IgnoreQueryFilters().Where(w => w.TenantId == TenantId && w.IsActive).ToDictionaryAsync(w => w.RequestTypeId, cancellationToken);
            _teams = await raw.Teams.IgnoreQueryFilters().Include(t => t.Members).Where(t => t.TenantId == TenantId).ToDictionaryAsync(t => t.Id, cancellationToken);
            _policies = await raw.SlaPolicies.IgnoreQueryFilters().Where(p => p.TenantId == TenantId).ToListAsync(cancellationToken);
            var calendar = await raw.BusinessCalendars.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.TenantId == TenantId, cancellationToken);
            _calendar = new BusinessTimeCalculator(calendar);
            _users = await raw.Users.IgnoreQueryFilters().Where(u => u.TenantId == TenantId && u.IsActive).ToListAsync(cancellationToken);

            var manager = _users.First(u => u.Roles.Contains(Role.Manager));
            foreach (var (first, last) in employees)
            {
                var user = User.Create($"{first}.{last}@{domain}".ToLowerInvariant(), first, last, [Role.Employee]);
                user.TenantId = TenantId;
                user.SetPasswordHash(passwordHash);
                user.SetManager(manager.Id);
                raw.Users.Add(user);
                _users.Add(user);
            }

            await raw.SaveChangesAsync(cancellationToken);
            _requesters = _users.Where(u => u.Roles.Length == 1 && u.Roles[0] == Role.Employee).ToList();
        }

        /// <summary>One to <paramref name="maxPerDay"/> cases per working day over the last six weeks, at office hours.</summary>
        private List<CasePlan> Plan(int maxPerDay)
        {
            (string Name, int Weight)[] mix =
            [
                ("Work certificate", 30), ("Leave request", 22), ("Payslip correction", 16), ("Training request", 11),
                ("Change of bank details", 9), ("Salary advance", 8),
            ];
            var plans = new List<CasePlan>();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _zone).DateTime);
            for (var daysBack = 42; daysBack >= 0; daysBack--)
            {
                var day = today.AddDays(-daysBack);
                var opening = Local(day, 8, 0);
                if (_calendar.BusinessMinutesBetween(opening, Local(day, 18, 0)) == 0)
                    continue;
                var count = Next(1, maxPerDay + 1);
                for (var i = 0; i < count; i++)
                {
                    var created = Local(day, Next(8, 16), Next(0, 60));
                    if (created >= now)
                        continue;
                    plans.Add(new CasePlan(created, Pick(mix), _requesters[Next(0, _requesters.Count)]));
                }
            }

            // One confidential report, handled by the restricted group.
            var reportDay = today.AddDays(-9);
            plans.Add(new CasePlan(Local(reportDay, 10, 20), "Harassment report", _requesters[Next(0, _requesters.Count)]));
            return plans;
        }

        private async Task CreateAsync(CasePlan plan, CancellationToken cancellationToken)
        {
            var ticket = await SubmitAsync(plan.Type, plan.Requester, plan.CreatedAt, cancellationToken);
            Live(ticket, plan.Requester, plan.CreatedAt);
            raw.Tickets.Add(ticket);
        }

        private async Task<Ticket> SubmitAsync(string typeName, User requester, DateTimeOffset createdAt, CancellationToken cancellationToken)
        {
            var type = _types[typeName];
            var (title, form) = Content(typeName, createdAt);
            var reference = await NextReferenceAsync(createdAt, cancellationToken);
            var ticket = Ticket.Submit(reference, type, requester.Id, title, Description(typeName), "{}", createdAt);
            ticket.TenantId = TenantId;
            foreach (var e in ticket.Events)
                e.TenantId = TenantId;
            ticket.CreatedAt = createdAt;

            foreach (var (field, fileName) in Files(typeName))
            {
                var bytes = Encoding.ASCII.GetBytes($"%PDF-1.4\n% Demo document {fileName} for {reference}\n%%EOF\n");
                using var content = new MemoryStream(bytes);
                var key = await storage.SaveAsync(TenantId, content, cancellationToken);
                var attachment = ticket.AddAttachment(fileName, "application/pdf", bytes.Length, key, requester.Id, field, createdAt);
                form[field] = new JsonArray(JsonValue.Create(attachment.Id.ToString()));
            }

            ticket.SetFormData(form.ToJsonString());

            if (_workflows.TryGetValue(type.Id, out var workflow))
                ticket.StartApproval(workflow, requester.ManagerId, createdAt);
            else
                Route(ticket, type, createdAt);

            var policy = _policies.FirstOrDefault(p => type.SlaPolicyId is { } id ? p.Id == id : p.IsDefault);
            if (policy is not null)
            {
                ticket.StartSla(policy, createdAt);
                ticket.RecalculateSla(_calendar, createdAt);
            }

            return ticket;
        }

        /// <summary>Moves the case forward step by step, each after some business time, until "now" is reached.</summary>
        private void Live(Ticket ticket, User requester, DateTimeOffset createdAt)
        {
            var at = createdAt;
            var type = _types.Values.First(t => t.Id == ticket.RequestTypeId);

            // Approvals.
            foreach (var step in ticket.Approvals.OrderBy(a => a.StepOrder).ToList())
            {
                if (!Step(ref at, 30, 600))
                    return;
                var decider = step.ApproverUserId is { } id ? _users.First(u => u.Id == id) : _users.First(u => u.Roles.Contains(step.ApproverRole));
                var reject = step.StepOrder == 1 && Chance(7);
                ticket.DecideApproval(step.Id, !reject, decider.Id, decider.Roles, reject ? "Not possible this month, let's discuss it." : null, at);
                Recalculate(ticket, at);
                if (reject)
                    return;
            }

            if (ticket.Approvals.Count > 0)
                Route(ticket, type, at);
            var agentId = ticket.AssigneeId ?? Claim(ticket, ref at);
            if (agentId is not { } agent)
                return;

            // Accept, then start work with a first reply.
            if (ticket.Status == TicketStatus.New)
            {
                if (!Step(ref at, 10, 180))
                    return;
                ticket.ChangeStatus(TicketStatus.Open, TransitionActor.Agent, agent, at);
                Recalculate(ticket, at);
            }

            if (!Step(ref at, 10, 240))
                return;
            ticket.AddComment(agent, Pick(StartReplies), isInternal: false, at);
            ticket.RecordFirstResponse(at);
            ticket.ChangeStatus(TicketStatus.InProgress, TransitionActor.Agent, agent, at);
            Recalculate(ticket, at);

            // The confidential report is still being investigated.
            if (ticket.IsConfidential)
                return;

            if (Chance(25))
            {
                if (!Step(ref at, 30, 300))
                    return;
                ticket.AddComment(agent, "Internal: checked with the team lead, fine to proceed.", isInternal: true, at);
            }

            if (Chance(15))
            {
                if (!Step(ref at, 30, 240))
                    return;
                var question = Pick(Questions);
                ticket.AddComment(agent, question, isInternal: false, at);
                ticket.ChangeStatus(TicketStatus.WaitingOnEmployee, TransitionActor.Agent, agent, at, question);
                Recalculate(ticket, at);
                if (!Step(ref at, 60, 900))
                    return;
                ticket.AddComment(requester.Id, "Here it is, thank you.", isInternal: false, at);
                ticket.ChangeStatus(TicketStatus.InProgress, TransitionActor.Requester, requester.Id, at);
                Recalculate(ticket, at);
            }

            // Resolution: most within the target, some late.
            var target = ticket.ResolutionTargetMinutes ?? 960;
            var factor = 0.2 + (_random.NextDouble() * 1.05);
            var remaining = Math.Max(30, (int)(target * factor) - _calendar.BusinessMinutesBetween(createdAt, at));
            if (!Step(ref at, remaining, remaining + 1))
                return;
            var resolution = Resolution(type.Name);
            ticket.AddComment(agent, resolution, isInternal: false, at);
            ticket.ChangeStatus(TicketStatus.Resolved, TransitionActor.Agent, agent, at, resolution);
            Recalculate(ticket, at);

            // The employee closes (and often rates), sometimes reopens; some stay resolved.
            if (!Step(ref at, 60, 1440))
                return;
            if (Chance(8))
            {
                ticket.AddComment(requester.Id, "Sorry, this is not solved yet: the amount is still wrong.", isInternal: false, at);
                ticket.ChangeStatus(TicketStatus.Reopened, TransitionActor.Requester, requester.Id, at);
                Recalculate(ticket, at);
                if (!Step(ref at, 60, 600))
                    return;
                ticket.ChangeStatus(TicketStatus.Resolved, TransitionActor.Agent, agent, at, "Corrected, sorry for the trouble.");
                Recalculate(ticket, at);
                if (!Step(ref at, 60, 900))
                    return;
            }

            if (Chance(20))
                return;
            ticket.ChangeStatus(TicketStatus.Closed, TransitionActor.Requester, requester.Id, at);
            Recalculate(ticket, at);
            if (Chance(72))
            {
                var score = Pick([(5, 42), (4, 33), (3, 14), (2, 7), (1, 4)]);
                _ratings.Add(SatisfactionRating.Create(ticket, requester.Id, score, Chance(45) ? Pick(RatingComments[score]) : null, at));
            }
        }

        /// <summary>A case near its deadline and one past it, both still open, so the monitor has work right away.</summary>
        private async Task HotCasesAsync(CancellationToken cancellationToken)
        {
            foreach (var (typeName, share) in new[] { ("Change of bank details", 0.85), ("Work certificate", 1.35) })
            {
                var type = _types[typeName];
                var policy = _policies.First(p => type.SlaPolicyId is { } id ? p.Id == id : p.IsDefault);
                var target = policy.TargetFor(type.DefaultPriority).ResolutionMinutes;
                var created = now.AddMinutes(-15);
                while (_calendar.BusinessMinutesBetween(created, now) < target * share)
                    created = created.AddMinutes(-15);

                var requester = _requesters[Next(0, _requesters.Count)];
                var ticket = await SubmitAsync(typeName, requester, created, cancellationToken);
                if (ticket.AssigneeId is { } agent)
                {
                    var at = _calendar.AddBusinessMinutes(created, 20);
                    ticket.ChangeStatus(TicketStatus.Open, TransitionActor.Agent, agent, at);
                    Recalculate(ticket, at);

                    // The at-risk one was answered in time: only its resolution deadline is getting close.
                    if (share < 1)
                    {
                        at = _calendar.AddBusinessMinutes(at, 20);
                        ticket.AddComment(agent, Pick(StartReplies), isInternal: false, at);
                        ticket.RecordFirstResponse(at);
                        ticket.ChangeStatus(TicketStatus.InProgress, TransitionActor.Agent, agent, at);
                        Recalculate(ticket, at);
                    }
                }

                raw.Tickets.Add(ticket);
            }
        }

        private async Task KnowledgeStatisticsAsync(CancellationToken cancellationToken)
        {
            foreach (var article in await raw.KnowledgeArticles.IgnoreQueryFilters().Where(a => a.TenantId == TenantId).ToListAsync(cancellationToken))
            {
                var views = Next(25, 180);
                for (var i = 0; i < views; i++)
                {
                    article.RecordView();
                    if (Chance(35))
                        article.RecordHelpful();
                }
            }
        }

        /// <summary>The responsible team, and a member chosen by the team's strategy (none for manual teams).</summary>
        private void Route(Ticket ticket, RequestType type, DateTimeOffset at)
        {
            if (type.ResponsibleTeamId is not { } teamId || !_teams.TryGetValue(teamId, out var team))
                return;
            ticket.MoveToTeam(team.Id, null, at);
            var members = team.OrderedMemberIds;
            if (team.Strategy == AssignmentStrategy.Manual || members.Count == 0)
                return;
            var turn = _roundRobin.GetValueOrDefault(team.Id);
            _roundRobin[team.Id] = turn + 1;
            ticket.Assign(members[turn % members.Count], null, at);
        }

        /// <summary>Manual teams: a member takes the case after a while.</summary>
        private Guid? Claim(Ticket ticket, ref DateTimeOffset at)
        {
            if (ticket.TeamId is not { } teamId || !_teams.TryGetValue(teamId, out var team) || team.OrderedMemberIds.Count == 0)
                return null;
            if (!Step(ref at, 20, 240))
                return null;
            var agent = team.OrderedMemberIds[0];
            ticket.Claim(agent, at);
            return agent;
        }

        private void Recalculate(Ticket ticket, DateTimeOffset at)
        {
            if (ticket.SlaStartedAt is not null)
                ticket.RecalculateSla(_calendar, at);
            ticket.UpdatedAt = at;
        }

        /// <summary>Advances by a random amount of business time; false when that would be in the future.</summary>
        private bool Step(ref DateTimeOffset at, int minMinutes, int maxMinutes)
        {
            var next = _calendar.AddBusinessMinutes(at, Next(minMinutes, Math.Max(minMinutes + 1, maxMinutes)));
            if (next >= now)
                return false;
            at = next;
            return true;
        }

        private async Task<string> NextReferenceAsync(DateTimeOffset createdAt, CancellationToken cancellationToken)
        {
            var year = TimeZoneInfo.ConvertTime(createdAt, _zone).Year;
            var next = (await raw.Database.SqlQuery<long>($"""
                INSERT INTO reference_counters (tenant_id, year, last_value)
                VALUES ({TenantId}, {year}, 1)
                ON CONFLICT (tenant_id, year) DO UPDATE SET last_value = reference_counters.last_value + 1
                RETURNING last_value AS "Value"
                """).ToListAsync(cancellationToken)).Single();
            return TicketReference.Format(year, next);
        }

        private DateTimeOffset Local(DateOnly day, int hour, int minute)
        {
            var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);
            return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
        }

        private int Next(int min, int max) => _random.Next(min, max);

        private bool Chance(int percent) => _random.Next(100) < percent;

        private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

        private T Pick<T>((T Value, int Weight)[] weighted)
        {
            var roll = _random.Next(weighted.Sum(w => w.Weight));
            foreach (var (value, weight) in weighted)
            {
                if (roll < weight)
                    return value;
                roll -= weight;
            }

            return weighted[^1].Value;
        }

        private (string Title, JsonObject Form) Content(string type, DateTimeOffset createdAt)
        {
            var local = TimeZoneInfo.ConvertTime(createdAt, _zone);
            var month = local.AddMonths(-1).ToString("MMMM", CultureInfo.InvariantCulture);
            var period = local.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var day = DateOnly.FromDateTime(local.DateTime);
            string Date(int days) => day.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var tunisia = tenant.Slug == "acme-tn";
            switch (type)
            {
                case "Work certificate":
                    var purpose = Pick(["bank", "visa", "housing"]);
                    return (purpose switch { "bank" => "Certificate for a bank loan", "visa" => "Certificate for a visa application", _ => "Certificate for my landlord" },
                        new JsonObject { ["purpose"] = purpose, ["language"] = Pick(["fr", "en", "ar"]), ["copies"] = Next(1, 3) });
                case "Payslip correction":
                    var issue = Pick(["missing_overtime", "wrong_deduction", "missing_bonus", "wrong_hours"]);
                    return (issue switch
                    {
                        "missing_overtime" => $"{month} payslip: overtime missing",
                        "wrong_deduction" => $"{month} payslip: wrong deduction",
                        "missing_bonus" => $"{month} payslip: bonus not paid",
                        _ => $"{month} payslip: wrong number of hours",
                    }, new JsonObject { ["payPeriod"] = period, ["issue"] = issue, ["expectedAmount"] = Next(40, 600) });
                case "Leave request":
                    var leave = Pick([("annual", 60), ("sick", 25), ("unpaid", 10), ("parental", 5)]);
                    var start = Next(3, 40);
                    return (leave switch { "annual" => "Annual leave", "sick" => "Sick leave", "unpaid" => "Unpaid leave", _ => "Paternity leave" },
                        new JsonObject { ["leaveType"] = leave, ["startDate"] = Date(start), ["endDate"] = Date(start + Next(1, 12)) });
                case "Training request":
                    var course = Pick(["Advanced Excel for HR", "Angular fundamentals", "Project management (PMP prep)", "Business English", "Data protection (GDPR) basics"]);
                    return ($"Training: {course}", new JsonObject
                    {
                        ["courseTitle"] = course, ["provider"] = Pick(["Coursera", "Orsys", "Cegos", "Local training centre"]),
                        ["startDate"] = Date(Next(20, 60)), ["cost"] = Next(300, 2500),
                        ["justification"] = "It will help me in my daily work and my team's projects.",
                    });
                case "Change of bank details":
                    return ("New bank account for my salary", new JsonObject
                    {
                        ["bankName"] = tunisia ? Pick(["BIAT", "Attijari Bank", "STB", "Amen Bank"]) : Pick(["BNP Paribas", "Société Générale", "Crédit Agricole"]),
                        ["iban"] = tunisia ? "TN5910006035183598478831" : "FR7630006000011234567890189",
                    });
                case "Salary advance":
                    var amount = Next(3, 20) * 100;
                    return ($"Salary advance of {amount}", new JsonObject
                    {
                        ["amount"] = amount, ["repaymentMonths"] = Next(2, 7), ["reason"] = Pick(["Unexpected car repair", "Medical expenses", "Moving to a new flat"]),
                    });
                default:
                    return ("Report about a colleague's behaviour", new JsonObject
                    {
                        ["incidentDate"] = Date(-3),
                        ["whatHappened"] = "Repeated inappropriate remarks during team meetings over the last weeks.",
                    });
            }
        }

        private static string Description(string type) => type switch
        {
            "Payslip correction" => "Please check, the amount does not match my timesheet.",
            "Leave request" => "My manager is aware.",
            "Harassment report" => "I would like this to stay confidential.",
            _ => string.Empty,
        };

        private static IEnumerable<(string Field, string FileName)> Files(string type) => type switch
        {
            "Payslip correction" => [("payslip", "payslip.pdf")],
            "Change of bank details" => [("bankCertificate", "rib.pdf")],
            _ => [],
        };

        private static string Resolution(string type) => type switch
        {
            "Work certificate" => "Your certificate is ready: you can download it from your documents or collect the signed copy at the HR office.",
            "Payslip correction" => "Checked with payroll: the difference will be paid with the next salary.",
            "Leave request" => "Your leave is registered in the HR system.",
            "Change of bank details" => "The new account is registered and applies from the next pay run.",
            "Training request" => "Your registration is confirmed; the invitation will follow by e-mail.",
            "Salary advance" => "The advance is approved and will be transferred within 48 hours.",
            _ => "The case has been handled by the restricted HR group.",
        };

        private static readonly string[] StartReplies =
        [
            "Thank you, we are looking into it.",
            "Received, I am taking care of it.",
            "Thanks for the details, we will get back to you shortly.",
        ];

        private static readonly string[] Questions =
        [
            "Could you send us your timesheet for that month?",
            "Could you confirm the exact dates, please?",
            "Could you attach the signed form, please?",
        ];

        private static readonly Dictionary<int, string[]> RatingComments = new()
        {
            [5] = ["Very fast, thank you!", "Perfect, exactly what I needed.", "Great service."],
            [4] = ["Good, thanks.", "Quick answer."],
            [3] = ["OK, but it took a while."],
            [2] = ["Too slow, I had to ask twice."],
            [1] = ["Still not happy with the answer."],
        };
    }
}
