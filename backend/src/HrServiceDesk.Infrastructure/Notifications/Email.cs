using Hangfire;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Notifications;
using HrServiceDesk.Infrastructure.Jobs;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HrServiceDesk.Infrastructure.Notifications;

/// <summary>Bound from the <c>Smtp</c> section. MailHog (no auth, no TLS) in development.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string From { get; set; } = "HR Service Desk <no-reply@hr-service-desk.local>";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseStartTls { get; set; }

    /// <summary>Base URL of the SPA, used for links in e-mails.</summary>
    public string AppUrl { get; set; } = "http://localhost:8080";
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var smtp = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(smtp.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}

/// <summary>Background job wrapper so e-mails are retried by Hangfire and never slow down a request.</summary>
public sealed class SendEmailJob(IEmailSender sender)
{
    public Task SendAsync(EmailMessage message) => sender.SendAsync(message, CancellationToken.None);
}

/// <summary>E-mails each notification to its recipient (queued in Hangfire when background jobs run).</summary>
internal sealed partial class EmailNotificationChannel(
    Persistence.AppDbContext db,
    IEmailSender sender,
    IOptions<SmtpOptions> options,
    IOptions<BackgroundJobOptions> jobs,
    IServiceProvider services,
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    public async Task DeliverAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return;

        var ids = notifications.Select(n => n.UserId).Distinct().ToList();
        var emails = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.IsActive)
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        foreach (var n in notifications)
        {
            if (!emails.TryGetValue(n.UserId, out var to))
                continue;
            var link = n.TicketId is { } ticketId ? $"\n\n{options.Value.AppUrl.TrimEnd('/')}/tickets/{ticketId}" : string.Empty;
            var message = new EmailMessage(to, n.Title, $"{n.Message}{link}\n\n— HR Service Desk");

            if (jobs.Value.Enabled)
                ((IBackgroundJobClient)services.GetService(typeof(IBackgroundJobClient))!).Enqueue<SendEmailJob>(j => j.SendAsync(message));
            else
                await SendInlineAsync(message, cancellationToken);
        }
    }

    private async Task SendInlineAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(message, cancellationToken);
        }
#pragma warning disable CA1031 // A mail server outage must not fail the request.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogSendFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "E-mail could not be sent")]
    private static partial void LogSendFailed(ILogger logger, Exception exception);
}
