using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class SsoConfigurationConfiguration : IEntityTypeConfiguration<SsoConfiguration>
{
    public void Configure(EntityTypeBuilder<SsoConfiguration> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.DisplayName).HasMaxLength(SsoConfiguration.DisplayNameMaxLength).IsRequired();
        builder.Property(c => c.Authority).HasMaxLength(SsoConfiguration.UrlMaxLength).IsRequired();
        builder.Property(c => c.MetadataAddress).HasMaxLength(SsoConfiguration.UrlMaxLength);
        builder.Property(c => c.ClientId).HasMaxLength(SsoConfiguration.ClientIdMaxLength).IsRequired();
        builder.Property(c => c.ProtectedClientSecret).HasMaxLength(1000).IsRequired();
        builder.PrimitiveCollection(c => c.EmailDomains).ElementType(e => e.HasMaxLength(255));
        builder.Ignore(c => c.EffectiveMetadataAddress);

        // One identity provider per organisation.
        builder.HasIndex(c => c.TenantId).IsUnique();
    }
}

internal sealed class SsoLoginAttemptConfiguration : IEntityTypeConfiguration<SsoLoginAttempt>
{
    public void Configure(EntityTypeBuilder<SsoLoginAttempt> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.State).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Nonce).HasMaxLength(64).IsRequired();
        builder.Property(a => a.CodeVerifier).HasMaxLength(128).IsRequired();
        builder.Property(a => a.ReturnUrl).HasMaxLength(500).IsRequired();
        builder.Ignore(a => a.CodeChallenge);
        builder.HasIndex(a => a.State).IsUnique();
        builder.HasIndex(a => a.ExpiresAt);
    }
}
