using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Audit;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Audit;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HrServiceDesk.Application.Gdpr;

public sealed record RetentionDto(int RetentionMonths);

public sealed record GetRetentionQuery : IRequest<RetentionDto>;

public sealed record SetRetentionCommand(int RetentionMonths) : IRequest<Result<RetentionDto>>;

/// <summary>
/// Anonymizes closed cases older than each organisation's retention period. <see cref="TenantId"/> limits the
/// run to one organisation (HR Admin "run now"); null processes all (the daily job).
/// </summary>
public sealed record RunRetentionCommand(Guid? TenantId) : IRequest<int>;

internal sealed partial class RetentionHandlers(
    IAppDbContext db,
    ITenantContext tenantContext,
    IPasswordHasher hasher,
    IFileStorage storage,
    AuditTrail audit,
    TimeProvider clock,
    ILogger<RetentionHandlers> logger)
    : IRequestHandler<GetRetentionQuery, RetentionDto>,
      IRequestHandler<SetRetentionCommand, Result<RetentionDto>>,
      IRequestHandler<RunRetentionCommand, int>
{
    private static readonly TicketStatus[] Closed = [TicketStatus.Closed, TicketStatus.Cancelled, TicketStatus.Rejected];

    public async Task<RetentionDto> Handle(GetRetentionQuery request, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        return new RetentionDto(tenant.RetentionMonths);
    }

    public async Task<Result<RetentionDto>> Handle(SetRetentionCommand request, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        var previous = tenant.RetentionMonths;
        tenant.SetRetention(request.RetentionMonths);
        audit.Add(AuditAction.RetentionPolicyChanged, "Tenant", tenant.Id, $"Retention changed from {previous} to {tenant.RetentionMonths} months");
        await db.SaveChangesAsync(cancellationToken);
        return new RetentionDto(tenant.RetentionMonths);
    }

    public async Task<int> Handle(RunRetentionCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var tenants = await db.Tenants.Where(t => request.TenantId == null || t.Id == request.TenantId).ToListAsync(cancellationToken);
        var anonymized = 0;

        foreach (var tenant in tenants)
        {
            var cutoff = now.AddMonths(-tenant.RetentionMonths);
            var ids = await db.Tickets.IgnoreQueryFilters()
                .Where(t => t.TenantId == tenant.Id && t.AnonymizedAt == null && Closed.Contains(t.Status)
                            && (t.SlaStoppedAt ?? t.UpdatedAt ?? t.CreatedAt) < cutoff)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
                continue;

            var formerEmployee = await FormerEmployeeAsync(tenant.Id, tenant.Slug, cancellationToken);
            foreach (var id in ids)
            {
                var ticket = await db.Tickets.IgnoreQueryFilters()
                    .Include(t => t.Comments).Include(t => t.Attachments).Include(t => t.Events).Include(t => t.Approvals)
                    .SingleAsync(t => t.Id == id, cancellationToken);

                var files = ticket.Anonymize(formerEmployee, now);
                // Notification texts may quote the case: they go too.
                db.Notifications.RemoveRange(await db.Notifications.IgnoreQueryFilters().Where(n => n.TicketId == id).ToListAsync(cancellationToken));
                audit.Add(AuditAction.CaseAnonymized, "Ticket", id, $"{ticket.Reference} anonymized after {tenant.RetentionMonths} months", tenant.Id);
                await db.SaveChangesAsync(cancellationToken);

                foreach (var key in files)
                    await DeleteQuietlyAsync(key);
                anonymized++;
            }
        }

        if (anonymized > 0)
            LogAnonymized(logger, anonymized);
        return anonymized;
    }

    /// <summary>The per-organisation placeholder that replaces requesters of anonymized cases (inactive, cannot sign in).</summary>
    private async Task<Guid> FormerEmployeeAsync(Guid tenantId, string slug, CancellationToken cancellationToken)
    {
        var email = $"former.employee@{slug}.invalid";
        var existing = await db.Users.IgnoreQueryFilters().Where(u => u.Email == email).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(cancellationToken);
        if (existing is { } id)
            return id;

        var user = User.Create(email, "Former", "employee", [Role.Employee]);
        user.TenantId = tenantId;
        user.SetPasswordHash(hasher.Hash(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));
        user.Deactivate();
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    private async Task DeleteQuietlyAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
#pragma warning disable CA1031 // A missing file must not stop the retention run.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention: {Count} closed case(s) anonymized")]
    private static partial void LogAnonymized(ILogger logger, int count);
}
