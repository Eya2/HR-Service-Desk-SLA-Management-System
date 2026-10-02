using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Infrastructure.Tickets;

/// <summary>
/// Increments the tenant's yearly counter with one <c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c>.
/// The row lock lasts until the caller's transaction ends, so concurrent submissions get consecutive
/// numbers and a rolled-back submission gives its number back.
/// </summary>
internal sealed class ReferenceNumberGenerator(AppDbContext db, TimeProvider clock) : IReferenceNumberGenerator
{
    public async Task<string> NextTicketReferenceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var timeZoneId = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.TimeZoneId).SingleAsync(cancellationToken);
        var year = LocalYear(timeZoneId);

        // INSERT ... RETURNING cannot be wrapped in a sub-query, so the single row is read client-side.
        var next = (await db.Database.SqlQuery<long>($"""
            INSERT INTO reference_counters (tenant_id, year, last_value)
            VALUES ({tenantId}, {year}, 1)
            ON CONFLICT (tenant_id, year) DO UPDATE SET last_value = reference_counters.last_value + 1
            RETURNING last_value AS "Value"
            """).ToListAsync(cancellationToken)).Single();

        return TicketReference.Format(year, next);
    }

    // The year rolls over at midnight in the organisation's own time zone.
    private int LocalYear(string timeZoneId)
    {
        var now = clock.GetUtcNow();
        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? TimeZoneInfo.ConvertTime(now, zone).Year
            : now.Year;
    }
}
