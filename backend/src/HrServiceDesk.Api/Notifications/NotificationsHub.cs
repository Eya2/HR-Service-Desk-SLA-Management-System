using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Domain.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HrServiceDesk.Api.Notifications;

/// <summary>Real-time channel: each signed-in user receives their own notifications (user = <c>sub</c> claim).</summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string Method = "notification";
}

internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst(AppClaims.Subject)?.Value;
}

/// <summary>Pushes each saved notification to its recipient's open browser tabs.</summary>
internal sealed class HubNotificationChannel(IHubContext<NotificationsHub> hub) : INotificationChannel
{
    public async Task DeliverAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken)
    {
        foreach (var n in notifications)
        {
            await hub.Clients.User(n.UserId.ToString()).SendAsync(
                NotificationsHub.Method,
                new { n.Id, Type = n.Type.ToString(), n.Title, n.Message, n.TicketId, n.CreatedAt, IsRead = false },
                cancellationToken);
        }
    }
}
