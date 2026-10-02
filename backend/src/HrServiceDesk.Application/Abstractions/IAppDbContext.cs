using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Abstractions;

/// <summary>Unit of work and query surface used by use-case handlers.</summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
