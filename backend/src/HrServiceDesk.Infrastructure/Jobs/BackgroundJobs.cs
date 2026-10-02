using Hangfire;
using Hangfire.PostgreSql;
using HrServiceDesk.Application.Escalations;
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

public static class BackgroundJobsSetup
{
    internal static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(BackgroundJobOptions.SectionName).Get<BackgroundJobOptions>() ?? new BackgroundJobOptions();
        services.AddScoped<SlaMonitorJob>();
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

        if (options.Dashboard)
            app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()] });
    }
}
