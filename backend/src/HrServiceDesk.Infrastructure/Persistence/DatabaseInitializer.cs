using HrServiceDesk.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HrServiceDesk.Infrastructure.Persistence;

public static partial class DatabaseInitializer
{
    /// <summary>Applies pending EF Core migrations, then seeds demo data when enabled. For Development and the Docker demo.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        LogPending(logger, pending.Count);
        await db.Database.MigrateAsync(cancellationToken);

        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} pending migration(s)")]
    private static partial void LogPending(ILogger logger, int count);
}
