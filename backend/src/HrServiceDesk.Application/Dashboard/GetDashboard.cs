using System.Globalization;
using System.Text;
using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Sla;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Application.Tickets.Assignment;
using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Dashboard;

/// <summary>The dashboard for [From, To) — the last 30 days by default — optionally for one team.</summary>
public sealed record GetDashboardQuery(DateTimeOffset? From, DateTimeOffset? To, Guid? TeamId) : IRequest<DashboardDto>;

/// <summary>The cases created in the period as CSV (one row per case).</summary>
public sealed record ExportDashboardQuery(DateTimeOffset? From, DateTimeOffset? To, Guid? TeamId) : IRequest<string>;

internal sealed class DashboardPeriodValidator : AbstractValidator<GetDashboardQuery>
{
    public DashboardPeriodValidator()
    {
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.From < q.To).WithMessage("The start must be before the end.");
        RuleFor(q => q).Must(q => q.From is null || q.To is null || (q.To.Value - q.From.Value).TotalDays <= 366)
            .WithMessage("The period can span at most one year.");
    }
}

internal sealed class ExportPeriodValidator : AbstractValidator<ExportDashboardQuery>
{
    public ExportPeriodValidator() =>
        RuleFor(q => q).Must(q => q.From is null || q.To is null || (q.From < q.To && (q.To.Value - q.From.Value).TotalDays <= 366))
            .WithMessage("The period must be valid and span at most one year.");
}

internal sealed class DashboardHandlers(IAppDbContext db, ICurrentUser currentUser, ITenantContext tenantContext, SlaService sla, TimeProvider clock)
    : IRequestHandler<GetDashboardQuery, DashboardDto>,
      IRequestHandler<ExportDashboardQuery, string>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = Period(request.From, request.To);
        var scope = Scope(request.TeamId);
        var calculator = await sla.CalculatorAsync(tenantContext.TenantId ?? Guid.Empty, cancellationToken);
        var zone = await TimeZoneAsync(cancellationToken);

        var created = await scope.Where(t => t.CreatedAt >= from && t.CreatedAt < to).ToListAsync(cancellationToken);
        var resolved = await scope.Where(t => t.ResolvedAt >= from && t.ResolvedAt < to).ToListAsync(cancellationToken);
        var backlog = await scope.Where(t => AutoAssigner.ActiveStatuses.Contains(t.Status))
            .Select(t => new { t.Status, t.Priority, t.SlaState, t.AssigneeId })
            .ToListAsync(cancellationToken);

        var types = await db.RequestTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Name).Select(t => new TeamOption(t.Id, t.Name)).ToListAsync(cancellationToken);
        var teamNames = teams.ToDictionary(t => t.Id, t => t.Name);

        // SLA compliance is measured on cases resolved in the period that had an SLA.
        var measured = resolved.Where(t => t.SlaStartedAt is not null).ToList();
        static double? Percent(int part, int whole) => whole == 0 ? null : Math.Round(100.0 * part / whole, 1);

        double? AverageHours(IEnumerable<int?> minutes)
        {
            var values = minutes.OfType<int>().ToList();
            return values.Count == 0 ? null : Math.Round(values.Average() / 60.0, 1);
        }

        // Satisfaction given in the period, on cases the caller may see.
        var ratings = await db.SatisfactionRatings.AsNoTracking()
            .Where(r => r.CreatedAt >= from && r.CreatedAt < to && scope.Any(t => t.Id == r.TicketId))
            .Select(r => r.Score)
            .ToListAsync(cancellationToken);

        var kpis = new DashboardKpis(
            created.Count,
            resolved.Count,
            backlog.Count,
            backlog.Count(t => t.SlaState == SlaState.AtRisk),
            backlog.Count(t => t.SlaState == SlaState.Breached),
            Percent(measured.Count(t => !t.ResolutionBreached), measured.Count),
            calculator is null ? null : AverageHours(created.Where(t => t.FirstRespondedAt is not null).Select(t => t.ElapsedBusinessMinutes(calculator, t.FirstRespondedAt!.Value))),
            calculator is null ? null : AverageHours(resolved.Select(t => t.ElapsedBusinessMinutes(calculator, t.ResolvedAt!.Value))),
            Percent(resolved.Count(t => t.ReopenCount > 0), resolved.Count),
            ratings.Count == 0 ? null : Math.Round(ratings.Average(), 1),
            ratings.Count);

        List<ComplianceRow> Compliance(Func<Ticket, string> key, Func<string, string> label) =>
            measured.GroupBy(key)
                .Select(g => new ComplianceRow(g.Key, label(g.Key), g.Count(), g.Count(t => !t.ResolutionBreached), Percent(g.Count(t => !t.ResolutionBreached), g.Count())))
                .OrderBy(r => r.CompliancePercent).ThenBy(r => r.Label)
                .ToList();

        var byType = Compliance(t => t.RequestTypeId.ToString(), k => types.TryGetValue(Guid.Parse(k), out var type) ? type.Name : k);
        var byTeam = Compliance(t => t.TeamId?.ToString() ?? "none", k => Guid.TryParse(k, out var id) && teamNames.TryGetValue(id, out var n) ? n : "No team");

        var byStatus = backlog.GroupBy(t => t.Status).OrderBy(g => g.Key)
            .Select(g => new NamedCount(g.Key.ToString(), g.Key.ToString(), g.Count())).ToList();
        var byPriority = Enum.GetValues<TicketPriority>()
            .Select(p => new NamedCount(p.ToString(), p.ToString(), backlog.Count(t => t.Priority == p))).ToList();
        var categories = created.GroupBy(t => types.TryGetValue(t.RequestTypeId, out var type) ? type.Category.ToString() : "Unknown")
            .Select(g => new NamedCount(g.Key, g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Key).ToList();

        var (granularity, volume) = Volume(created, resolved, from, to, zone);

        var activeByAgent = backlog.Where(t => t.AssigneeId is not null).GroupBy(t => t.AssigneeId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var resolvedByAgent = resolved.Where(t => t.AssigneeId is not null).GroupBy(t => t.AssigneeId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var agentIds = activeByAgent.Keys.Union(resolvedByAgent.Keys).ToList();
        var agentNames = await db.Users.AsNoTracking().Where(u => agentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, cancellationToken);
        var workload = agentIds
            .Select(id => new AgentWorkload(id, agentNames.GetValueOrDefault(id, "Unknown"), activeByAgent.GetValueOrDefault(id), resolvedByAgent.GetValueOrDefault(id)))
            .OrderByDescending(a => a.ActiveCases).ThenBy(a => a.Name)
            .ToList();

        return new DashboardDto(from, to, request.TeamId, granularity, kpis, byType, byTeam, byStatus, byPriority, categories, volume, workload, teams);
    }

    public async Task<string> Handle(ExportDashboardQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = Period(request.From, request.To);
        var tickets = await Scope(request.TeamId).Where(t => t.CreatedAt >= from && t.CreatedAt < to)
            .OrderBy(t => t.CreatedAt).ToListAsync(cancellationToken);
        var types = await db.RequestTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
        var teams = await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
        var userIds = tickets.Where(t => t.AssigneeId is not null).Select(t => t.AssigneeId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, cancellationToken);
        var calculator = await sla.CalculatorAsync(tenantContext.TenantId ?? Guid.Empty, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Reference,Request type,Category,Priority,Status,Team,Assignee,Created (UTC),First response (UTC),Resolved (UTC),"
                       + "First response (business h),Resolution (business h),SLA state,First response breached,Resolution breached,Reopened");
        foreach (var t in tickets)
        {
            var type = types.GetValueOrDefault(t.RequestTypeId);
            string Hours(DateTimeOffset? at) => at is { } instant && calculator is not null && t.ElapsedBusinessMinutes(calculator, instant) is { } m
                ? (m / 60.0).ToString("0.0", CultureInfo.InvariantCulture)
                : string.Empty;
            string[] cells =
            [
                t.Reference, type?.Name ?? string.Empty, type?.Category.ToString() ?? string.Empty, t.Priority.ToString(), t.Status.ToString(),
                t.TeamId is { } teamId ? teams.GetValueOrDefault(teamId, string.Empty) : string.Empty,
                t.AssigneeId is { } assignee ? users.GetValueOrDefault(assignee, string.Empty) : string.Empty,
                Iso(t.CreatedAt), Iso(t.FirstRespondedAt), Iso(t.ResolvedAt), Hours(t.FirstRespondedAt), Hours(t.ResolvedAt),
                t.SlaState.ToString(), Yes(t.FirstResponseBreached), Yes(t.ResolutionBreached), t.ReopenCount.ToString(CultureInfo.InvariantCulture),
            ];
            csv.AppendLine(string.Join(',', cells.Select(Cell)));
        }

        return csv.ToString();
    }

    /// <summary>Cases the caller may see (confidential ones only for the restricted group), optionally one team.</summary>
    private IQueryable<Ticket> Scope(Guid? teamId)
    {
        var query = db.Tickets.AsNoTracking().VisibleTo(db, currentUser);
        return teamId is { } id ? query.Where(t => t.TeamId == id) : query;
    }

    private (DateTimeOffset From, DateTimeOffset To) Period(DateTimeOffset? from, DateTimeOffset? to)
    {
        // PostgreSQL timestamptz parameters must be UTC; callers may send any offset.
        var end = (to ?? clock.GetUtcNow()).ToUniversalTime();
        return ((from ?? end.AddDays(-30)).ToUniversalTime(), end);
    }

    private async Task<TimeZoneInfo> TimeZoneAsync(CancellationToken cancellationToken)
    {
        var id = await db.Tenants.Where(t => t.Id == tenantContext.TenantId).Select(t => t.TimeZoneId).SingleOrDefaultAsync(cancellationToken);
        return id is not null && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Utc;
    }

    /// <summary>Daily buckets up to 92 days, weekly (Monday) beyond, in the organisation's time zone.</summary>
    private static (string Granularity, List<VolumePoint> Points) Volume(
        List<Ticket> created, List<Ticket> resolved, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone)
    {
        var weekly = (to - from).TotalDays > 92;
        DateOnly Bucket(DateTimeOffset at)
        {
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
            return weekly ? day.AddDays(-(((int)day.DayOfWeek + 6) % 7)) : day;
        }

        var createdBy = created.GroupBy(t => Bucket(t.CreatedAt)).ToDictionary(g => g.Key, g => g.Count());
        var resolvedBy = resolved.GroupBy(t => Bucket(t.ResolvedAt!.Value)).ToDictionary(g => g.Key, g => g.Count());
        var points = new List<VolumePoint>();
        for (var day = Bucket(from); day <= Bucket(to.AddTicks(-1)); day = day.AddDays(weekly ? 7 : 1))
            points.Add(new VolumePoint(day, createdBy.GetValueOrDefault(day), resolvedBy.GetValueOrDefault(day)));
        return (weekly ? "Week" : "Day", points);
    }

    private static string Iso(DateTimeOffset? at) => at?.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Yes(bool value) => value ? "yes" : "no";

    /// <summary>RFC 4180 quoting, and a leading quote on formula-like text so spreadsheets never execute it.</summary>
    internal static string Cell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }
}
