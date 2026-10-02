namespace HrServiceDesk.Infrastructure.Tickets;

/// <summary>Last case number issued for a tenant and year. Only touched through an atomic upsert.</summary>
internal sealed class ReferenceCounter
{
    public Guid TenantId { get; set; }

    public int Year { get; set; }

    public long LastValue { get; set; }
}
