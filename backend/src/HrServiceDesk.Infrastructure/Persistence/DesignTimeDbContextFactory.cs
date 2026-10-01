using HrServiceDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HrServiceDesk.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only. Set HRDESK_DESIGN_CONNECTION to point at a real database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HRDESK_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=hrdesk;Username=hrdesk;Password=hrdesk";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
