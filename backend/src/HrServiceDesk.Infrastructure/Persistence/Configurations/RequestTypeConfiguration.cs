using HrServiceDesk.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class RequestTypeConfiguration : IEntityTypeConfiguration<RequestType>
{
    public void Configure(EntityTypeBuilder<RequestType> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(RequestType.NameMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(RequestType.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.DefaultPriority).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.FormSchemaJson).HasColumnName("form_schema").HasColumnType("jsonb").IsRequired();
        builder.Ignore(t => t.Schema);
        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
    }
}
