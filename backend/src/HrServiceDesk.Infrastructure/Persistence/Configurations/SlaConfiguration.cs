using HrServiceDesk.Domain.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class BusinessCalendarConfiguration : IEntityTypeConfiguration<BusinessCalendar>
{
    public void Configure(EntityTypeBuilder<BusinessCalendar> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(BusinessCalendar.NameMaxLength).IsRequired();
        builder.Property(c => c.TimeZoneId).HasMaxLength(64).IsRequired();
        // One calendar per organisation.
        builder.HasIndex(c => c.TenantId).IsUnique();

        builder.OwnsMany(c => c.WorkingHours, hours => hours.ToJson());
        builder.OwnsMany(c => c.Holidays, holiday =>
        {
            holiday.ToTable("holidays");
            holiday.WithOwner().HasForeignKey("CalendarId");
            holiday.HasKey("CalendarId", nameof(Holiday.Date));
            holiday.Property(h => h.Name).HasMaxLength(100).IsRequired();
        });
    }
}

internal sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(SlaPolicy.NameMaxLength).IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
        builder.PrimitiveCollection(p => p.PauseStatuses).ElementType(e => e.HasConversion<string>().HasMaxLength(32));
        builder.OwnsMany(p => p.Targets, targets =>
        {
            targets.ToJson();
            targets.Property(t => t.Priority).HasConversion<string>();
        });
    }
}
