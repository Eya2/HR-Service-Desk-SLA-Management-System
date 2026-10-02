using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Notifications;
using HrServiceDesk.Domain.Notifications;
using HrServiceDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HrServiceDesk.Infrastructure.Notifications;

/// <summary>
/// Turns new audit events into notifications in the same transaction as the change, then hands the
/// saved notifications to the delivery channels (push, e-mail). Delivery failures are logged, never thrown.
/// </summary>
public sealed partial class NotificationInterceptor(IServiceProvider services, ILogger<NotificationInterceptor> logger) : SaveChangesInterceptor
{
    private readonly List<Notification> _pending = [];

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

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await DeliverAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private async Task PlanAsync(DbContext context, CancellationToken cancellationToken)
    {
        var events = context.ChangeTracker.Entries<TicketEvent>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();
        if (events.Count == 0)
            return;

        var tickets = context.ChangeTracker.Entries<Ticket>().Select(e => e.Entity).ToDictionary(t => t.Id);
        var planner = services.GetRequiredService<NotificationPlanner>();
        var notifications = await planner.PlanAsync(events, tickets, cancellationToken);
        context.AddRange(notifications);
        _pending.AddRange(notifications);
    }

    private async Task DeliverAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0)
            return;
        var batch = _pending.ToList();
        _pending.Clear();

        foreach (var channel in services.GetServices<INotificationChannel>())
        {
            try
            {
                await channel.DeliverAsync(batch, cancellationToken);
            }
#pragma warning disable CA1031 // Delivery is best effort: the business change is already committed.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogDeliveryFailed(logger, ex, channel.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification channel {Channel} failed")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, string channel);
}
