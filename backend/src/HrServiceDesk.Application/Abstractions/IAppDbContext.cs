using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Tenants;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HrServiceDesk.Application.Abstractions;

/// <summary>Unit of work and query surface used by use-case handlers.</summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<RequestType> RequestTypes { get; }

    DbSet<Ticket> Tickets { get; }

    DbSet<Comment> Comments { get; }

    DbSet<Attachment> Attachments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts a transaction spanning raw SQL (e.g. reference allocation) and <see cref="SaveChangesAsync"/>.</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
