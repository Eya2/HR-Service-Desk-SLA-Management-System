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

/// <summary>Sends e-mails off the request path (a background job when jobs run). Never throws for delivery problems.</summary>
public interface IEmailOutbox
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Absolute links into the web application, for e-mails.</summary>
public interface IAppLinks
{
    string Ticket(Guid ticketId);

    string PasswordReset(string email, string token);

    /// <summary>The OpenID Connect redirect URI to register at the identity provider.</summary>
    string SsoCallback();

    /// <summary>A page of the web app (path starting with '/').</summary>
    string App(string path);
}
