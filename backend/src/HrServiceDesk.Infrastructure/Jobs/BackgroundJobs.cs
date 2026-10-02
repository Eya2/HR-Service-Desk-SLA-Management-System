using Hangfire;
using Hangfire.PostgreSql;
using HrServiceDesk.Application.Escalations;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Gdpr;
using HrServiceDesk.Application.Integration;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrServiceDesk.Infrastructure.Jobs;

/// <summary>Bound from <c>BackgroundJobs</c>. Off in tests, which run jobs directly.</summary>
public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; }

    /// <summary>Exposes the Hangfire dashboard at /hangfire to local requests only.</summary>
    public bool Dashboard { get; set; }
}

/// <summary>Runs the SLA monitor as a recurring Hangfire job (every minute).</summary>
public sealed class SlaMonitorJob(ISender sender)
{
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync() => sender.Send(new RunSlaMonitorCommand());
}

/// <summary>Anonymizes closed cases past their organisation's retention period (daily, 02:00 UTC).</summary>
public sealed class RetentionJob(ISender sender)
{
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task RunAsync() => sender.Send(new RunRetentionCommand(TenantId: null));
}

/// <summary>Sends due webhook deliveries: every minute, and right after a change queues some.</summary>
public sealed class WebhookDispatchJob(ISender sender)
{
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        // Batches until nothing is due, so a burst does not wait for the next minute.
        while (await sender.Send(new DispatchWebhooksCommand()) == 50)
        {
        }
    }
}

/// <summary>Enqueues a dispatch run when background jobs are on; otherwise the recurring sweep (or a test) sends them.</summary>
internal sealed class WebhookDispatchTrigger(IServiceProvider services) : IWebhookDispatchTrigger
{
    public void Kick()
    {
        if (services.GetService(typeof(IBackgroundJobClient)) is IBackgroundJobClient jobs)
            jobs.Enqueue<WebhookDispatchJob>(j => j.RunAsync());
    }
}

public static class BackgroundJobsSetup
{
    internal static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(BackgroundJobOptions.SectionName).Get<BackgroundJobOptions>() ?? new BackgroundJobOptions();
        services.AddScoped<SlaMonitorJob>();
        services.AddScoped<RetentionJob>();
        services.AddScoped<WebhookDispatchJob>();
        services.AddScoped<IWebhookDispatchTrigger, WebhookDispatchTrigger>();
        if (!options.Enabled)
            return;

        var connectionString = configuration.GetConnectionString(DependencyInjection.ConnectionStringName)!;
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
        services.AddHangfireServer(o => o.WorkerCount = 2);
    }

    /// <summary>Registers recurring jobs (and the dashboard when enabled). Call once at startup.</summary>
    public static void UseBackgroundJobs(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = app.Configuration.GetSection(BackgroundJobOptions.SectionName).Get<BackgroundJobOptions>() ?? new BackgroundJobOptions();
        if (!options.Enabled)
            return;

        app.Services.GetRequiredService<IRecurringJobManager>()
            .AddOrUpdate<SlaMonitorJob>("sla-monitor", job => job.RunAsync(), Cron.Minutely());
        app.Services.GetRequiredService<IRecurringJobManager>()
            .AddOrUpdate<RetentionJob>("retention", job => job.RunAsync(), Cron.Daily(2));
        app.Services.GetRequiredService<IRecurringJobManager>()
            .AddOrUpdate<WebhookDispatchJob>("webhooks", job => job.RunAsync(), Cron.Minutely());

        if (options.Dashboard)
            app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()] });
    }
}
