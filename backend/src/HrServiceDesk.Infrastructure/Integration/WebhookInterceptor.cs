using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Integration;
using HrServiceDesk.Domain.Integration;
using HrServiceDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace HrServiceDesk.Infrastructure.Integration;

/// <summary>
/// Writes webhook deliveries for new case events in the same transaction (outbox), then asks for them to be
/// sent once the change is committed.
/// </summary>
public sealed class WebhookInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    private bool _planned;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
            await PlanAsync(context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
            PlanAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Kick();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Kick();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _planned = false;
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _planned = false;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private async Task PlanAsync(DbContext context, CancellationToken cancellationToken)
    {
        var events = context.ChangeTracker.Entries<TicketEvent>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        if (events.Count == 0)
            return;

        var tickets = context.ChangeTracker.Entries<Ticket>().Select(e => e.Entity).ToDictionary(t => t.Id);
        var deliveries = await services.GetRequiredService<WebhookPlanner>().PlanAsync(events, tickets, cancellationToken);
        if (deliveries.Count == 0)
            return;
        context.Set<WebhookDelivery>().AddRange(deliveries);
        _planned = true;
    }

    private void Kick()
    {
        if (!_planned)
            return;
        _planned = false;
        services.GetRequiredService<IWebhookDispatchTrigger>().Kick();
    }
}
