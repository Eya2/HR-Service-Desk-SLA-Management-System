using HrServiceDesk.Domain.Integration;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Name).HasMaxLength(ApiKey.NameMaxLength).IsRequired();
        builder.Property(k => k.Prefix).HasMaxLength(16).IsRequired();
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.PrimitiveCollection(k => k.Scopes).ElementType(e => e.HasMaxLength(32));
        builder.Ignore(k => k.IsActive);

        // Keys are looked up by prefix before the tenant is known.
        builder.HasIndex(k => k.Prefix).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(k => k.ServiceUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(WebhookSubscription.NameMaxLength).IsRequired();
        builder.Property(s => s.Url).HasMaxLength(WebhookSubscription.UrlMaxLength).IsRequired();
        builder.Property(s => s.ProtectedSecret).HasMaxLength(256).IsRequired();
        builder.PrimitiveCollection(s => s.Events).ElementType(e => e.HasMaxLength(48));
        builder.HasIndex(s => new { s.TenantId, s.IsActive });
    }
}

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.EventType).HasMaxLength(48).IsRequired();
        builder.Property(d => d.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.LastError).HasMaxLength(WebhookDelivery.ErrorMaxLength);

        // The dispatcher's sweep, and the admin screen's latest-first list.
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt });
        builder.HasIndex(d => new { d.SubscriptionId, d.CreatedAt });
        builder.HasOne<WebhookSubscription>().WithMany().HasForeignKey(d => d.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}
