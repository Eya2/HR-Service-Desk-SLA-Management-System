using System.Collections.Concurrent;
using HrServiceDesk.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>Captures e-mails instead of sending them.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

/// <summary>Captures webhook requests instead of sending them; <see cref="Respond"/> decides the outcome.</summary>
public sealed class RecordingWebhookSender : IWebhookSender
{
    public ConcurrentQueue<WebhookRequest> Sent { get; } = new();

    public Func<WebhookRequest, WebhookResponse> Respond { get; set; } = _ => new WebhookResponse(true, 200, null);

    public Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken cancellationToken)
    {
        Sent.Enqueue(request);
        return Task.FromResult(Respond(request));
    }
}

/// <summary>
/// An API host whose application clock is a <see cref="TestClock"/>, for time-based behaviour (SLA, jobs).
/// Tokens are stamped with that clock, so their lifetime is not checked against the real one here.
/// </summary>
public sealed class ClockedApiFactory(string connectionString, DateTimeOffset start)
    : ApiFactory(connectionString, new Dictionary<string, string> { ["Smtp:Enabled"] = "true" })
{
    public TestClock Clock { get; } = new(start);

    /// <summary>E-mails "sent" by this host.</summary>
    public RecordingEmailSender Emails { get; } = new();

    /// <summary>Webhook requests "sent" by this host.</summary>
    public RecordingWebhookSender Webhooks { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.RemoveAll<IWebhookSender>();
            services.AddSingleton<IWebhookSender>(Webhooks);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.TokenValidationParameters.ValidateLifetime = false);
        });
    }
}
