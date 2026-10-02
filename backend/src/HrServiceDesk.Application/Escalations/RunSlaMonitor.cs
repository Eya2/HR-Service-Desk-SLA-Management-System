using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Sla;
using HrServiceDesk.Application.Tickets.Assignment;
using HrServiceDesk.Domain.Escalations;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HrServiceDesk.Application.Escalations;

public sealed record SlaMonitorReport(int CasesChecked, int StateChanges, int Escalations, int Conflicts);

/// <summary>
/// The recurring SLA monitor (every minute), for every organisation: refreshes the SLA state of running
/// cases and fires escalation rules. A rule fires once per case (unique execution record). A case being
/// edited at the same moment is skipped and picked up on the next run.
/// </summary>
public sealed record RunSlaMonitorCommand(int BatchSize = 200) : IRequest<SlaMonitorReport>;

internal sealed partial class RunSlaMonitorHandler(
    IAppDbContext db,
    SlaService sla,
    AutoAssigner autoAssigner,
    TimeProvider clock,
    ILogger<RunSlaMonitorHandler> logger) : IRequestHandler<RunSlaMonitorCommand, SlaMonitorReport>
{
    public async Task<SlaMonitorReport> Handle(RunSlaMonitorCommand request, CancellationToken cancellationToken)
    {
        var rules = await db.EscalationRules.IgnoreQueryFilters().Where(r => r.IsActive).ToListAsync(cancellationToken);
        var rulesByTenant = rules.ToLookup(r => r.TenantId);
        int checkedCount = 0, changes = 0, escalations = 0, conflicts = 0;
        Guid? after = null;

        while (true)
        {
            // Running clocks only: started, not stopped, not resolved (the clock is paused there).
            var ids = await db.Tickets.IgnoreQueryFilters()
                .Where(t => t.SlaStartedAt != null && t.SlaStoppedAt == null && t.Status != TicketStatus.Resolved)
                .Where(t => after == null || t.Id.CompareTo(after.Value) > 0)
                .OrderBy(t => t.Id)
                .Select(t => t.Id)
                .Take(request.BatchSize)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
                break;
            after = ids[^1];

            var fired = (await db.EscalationExecutions.IgnoreQueryFilters()
                    .Where(x => ids.Contains(x.TicketId))
                    .Select(x => new { x.TicketId, x.RuleId })
                    .ToListAsync(cancellationToken))
                .Select(x => (x.TicketId, x.RuleId))
                .ToHashSet();

            // One case at a time: a conflict on one case never loses the work done on the others.
            foreach (var id in ids)
            {
                var ticket = await db.Tickets.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
                if (ticket is null)
                    continue;
                checkedCount++;
                var (previous, current) = await sla.RefreshAsync(ticket, cancellationToken);
                if (previous != current)
                    changes++;

                var calculator = await sla.CalculatorAsync(ticket.TenantId, cancellationToken);
                var facts = new EscalationFacts(
                    ticket.RequestTypeId, ticket.SlaState,
                    calculator is null ? null : ticket.BusinessMinutesWithoutResponse(calculator, clock.GetUtcNow()),
                    ticket.IsFinal);

                foreach (var rule in rulesByTenant[ticket.TenantId].Where(r => !fired.Contains((ticket.Id, r.Id)) && r.IsTriggeredBy(facts)))
                {
                    await ApplyAsync(rule, ticket, cancellationToken);
                    db.EscalationExecutions.Add(new EscalationExecution(ticket.TenantId, ticket.Id, rule.Id, clock.GetUtcNow()));
                    escalations++;
                }

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    // Edited concurrently, or the rule fired from another monitor instance: retry next run.
                    conflicts++;
                    LogConflict(logger, ticket.Reference, ex.GetType().Name);
                }

                ClearTracker();
            }
        }

        if (changes + escalations > 0)
            LogRun(logger, checkedCount, changes, escalations);
        return new SlaMonitorReport(checkedCount, changes, escalations, conflicts);
    }

    private async Task ApplyAsync(EscalationRule rule, Ticket ticket, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        switch (rule.Action)
        {
            case EscalationAction.BumpPriority:
                if (ticket.RaisePriority(now))
                    await sla.RetargetAsync(ticket, cancellationToken);
                break;
            case EscalationAction.ReassignToTeam when rule.TargetTeamId is { } teamId:
                ticket.MoveToTeam(teamId, actorId: null, now);
                await autoAssigner.AssignAsync(ticket, cancellationToken);
                break;
        }

        // Notifications (assignee, manager, …) are derived from this event by the notification planner.
        ticket.Record(TicketEventType.Escalated, null, now, new
        {
            ruleId = rule.Id,
            rule = rule.Name,
            trigger = rule.Trigger.ToString(),
            action = rule.Action.ToString(),
        });
    }

    private void ClearTracker()
    {
        if (db is DbContext context)
            context.ChangeTracker.Clear();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SLA monitor skipped {Reference} this run ({Error})")]
    private static partial void LogConflict(ILogger logger, string reference, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "SLA monitor: {Checked} cases checked, {Changes} state changes, {Escalations} escalations")]
    private static partial void LogRun(ILogger logger, int @checked, int changes, int escalations);
}
