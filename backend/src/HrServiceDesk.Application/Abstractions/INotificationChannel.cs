using HrServiceDesk.Domain.Notifications;

namespace HrServiceDesk.Application.Abstractions;

/// <summary>Delivers saved notifications (real-time push, e-mail). Failures must not undo the business change.</summary>
public interface INotificationChannel
{
    Task DeliverAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string Body);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
