using System.Reflection;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : this((DbContextOptions)options, tenantContext)
    {
    }

    /// <summary>For derived contexts (tests) that bring their own options type.</summary>
    protected AppDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Read by the global query filters. EF Core re-evaluates context members on every query,
    /// so a pooled or long-lived model still filters by the tenant of the current scope.
    /// A <c>null</c> tenant matches no tenant-owned rows (secure default).
    /// </summary>
    public Guid? CurrentTenantId => _tenantContext.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType) && entityType.BaseType is null)
            {
                ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<TEntity>().HasIndex(e => e.TenantId);
    }
}
