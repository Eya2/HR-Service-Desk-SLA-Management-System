using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>The caller's in-app notifications (also pushed live on /hubs/notifications).</summary>
[Route("api/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListNotificationsQuery(unreadOnly, page, pageSize), cancellationToken));

    [HttpGet("unread-count")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new UnreadCountQuery(), cancellationToken));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MarkRead(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new MarkNotificationReadCommand(id), cancellationToken));

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new MarkAllNotificationsReadCommand(), cancellationToken));
}
