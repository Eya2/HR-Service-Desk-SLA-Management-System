using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HrServiceDesk.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps <see cref="ITenantOwned.TenantId"/> on insert and refuses writes that cross tenants.
/// When no tenant is resolved (seeding, SuperAdmin jobs), the caller must set TenantId explicitly.
/// </summary>
public sealed class TenantStampingInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
            return;

        var currentTenant = tenantContext.TenantId;

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.TenantId == Guid.Empty)
                    {
                        entry.Entity.TenantId = currentTenant
                            ?? throw new InvalidOperationException(
                                $"Cannot insert {entry.Metadata.ClrType.Name}: no tenant is set for this operation.");
                    }
                    else
                    {
                        EnsureSameTenant(entry.Entity.TenantId, currentTenant, entry.Metadata.ClrType.Name);
                    }

                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    if (entry.Property(e => e.TenantId).IsModified)
                        throw new InvalidOperationException($"TenantId of {entry.Metadata.ClrType.Name} cannot be changed.");
                    EnsureSameTenant(entry.Entity.TenantId, currentTenant, entry.Metadata.ClrType.Name);
                    break;
            }
        }
    }

    private static void EnsureSameTenant(Guid entityTenant, Guid? currentTenant, string entityName)
    {
        if (currentTenant is { } tenant && tenant != entityTenant)
            throw new InvalidOperationException($"Cross-tenant write to {entityName} was blocked.");
    }
}
