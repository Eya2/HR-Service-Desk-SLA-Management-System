using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Sla;

/// <summary>
/// Starts and refreshes case SLA clocks with the tenant's calendar and policies. Calendars and policies
/// are looked up by the case's own tenant, so the background monitor can use it without a tenant context.
/// The caller saves.
/// </summary>
internal sealed class SlaService(IAppDbContext db, ITenantContext tenantContext, TimeProvider clock)
{
    private readonly Dictionary<Guid, IBusinessTimeCalculator?> _calculators = [];

    /// <summary>Starts the clock at submission, with the request type's policy or the tenant's default one.</summary>
    public async Task StartAsync(Ticket ticket, RequestType type, CancellationToken cancellationToken)
    {
        var tenantId = TenantOf(ticket);
        var policy = await db.SlaPolicies.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && (type.SlaPolicyId == null ? p.IsDefault : p.Id == type.SlaPolicyId))
            .FirstOrDefaultAsync(cancellationToken);
        if (policy is null || await CalculatorAsync(tenantId, cancellationToken) is null)
            return;

        ticket.StartSla(policy, clock.GetUtcNow());
        await RefreshAsync(ticket, cancellationToken);
    }

    /// <summary>Recomputes deadlines and state; returns the change, if any.</summary>
    public async Task<(SlaState Previous, SlaState Current)> RefreshAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        if (ticket.SlaStartedAt is null || await CalculatorAsync(TenantOf(ticket), cancellationToken) is not { } calculator)
            return (ticket.SlaState, ticket.SlaState);
        return ticket.RecalculateSla(calculator, clock.GetUtcNow());
    }

    /// <summary>After a priority change: the targets of the new priority apply.</summary>
    public async Task RetargetAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        if (ticket.SlaPolicyId is not { } policyId)
            return;
        var policy = await db.SlaPolicies.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == policyId, cancellationToken);
        if (policy is null)
            return;
        ticket.Retarget(policy);
        await RefreshAsync(ticket, cancellationToken);
    }

    public async Task<IBusinessTimeCalculator?> CalculatorAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_calculators.TryGetValue(tenantId, out var calculator))
        {
            var calendar = await db.BusinessCalendars.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
            calculator = calendar is null ? null : new BusinessTimeCalculator(calendar);
            _calculators[tenantId] = calculator;
        }

        return calculator;
    }

    // A case being submitted has no TenantId until it is saved.
    private Guid TenantOf(Ticket ticket) =>
        ticket.TenantId != Guid.Empty ? ticket.TenantId : tenantContext.TenantId ?? Guid.Empty;
}
