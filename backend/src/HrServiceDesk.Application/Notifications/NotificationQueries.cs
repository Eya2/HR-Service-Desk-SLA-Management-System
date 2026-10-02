using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Notifications;

public sealed record NotificationDto(Guid Id, string Type, string Title, string Message, Guid? TicketId, DateTimeOffset CreatedAt, bool IsRead);

/// <summary>The caller's notifications, newest first.</summary>
public sealed record ListNotificationsQuery(bool UnreadOnly, int Page = 1, int PageSize = 20) : IRequest<PagedResult<NotificationDto>>;

internal sealed class ListNotificationsValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed record UnreadCountQuery : IRequest<int>;

public sealed record MarkNotificationReadCommand(Guid Id) : IRequest<Result>;

public sealed record MarkAllNotificationsReadCommand : IRequest<Result>;

internal sealed class NotificationHandlers(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IRequestHandler<ListNotificationsQuery, PagedResult<NotificationDto>>,
      IRequestHandler<UnreadCountQuery, int>,
      IRequestHandler<MarkNotificationReadCommand, Result>,
      IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private Guid Me => currentUser.UserId ?? Guid.Empty;

    public async Task<PagedResult<NotificationDto>> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == Me);
        if (request.UnreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Message, n.TicketId, n.CreatedAt, n.ReadAt != null))
            .ToListAsync(cancellationToken);
        return new PagedResult<NotificationDto>(items, request.Page, request.PageSize, total);
    }

    public Task<int> Handle(UnreadCountQuery request, CancellationToken cancellationToken) =>
        db.Notifications.CountAsync(n => n.UserId == Me && n.ReadAt == null, cancellationToken);

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == request.Id && n.UserId == Me, cancellationToken);
        if (notification is null)
            return Error.NotFound("notification.not_found", "Notification not found.");
        notification.MarkRead(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        foreach (var notification in await db.Notifications.Where(n => n.UserId == Me && n.ReadAt == null).ToListAsync(cancellationToken))
            notification.MarkRead(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
