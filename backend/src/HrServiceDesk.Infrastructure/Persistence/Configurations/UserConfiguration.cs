using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Ignore(u => u.FullName);

        // Stored as a PostgreSQL text[] of role names.
        builder.PrimitiveCollection(u => u.Roles)
            .ElementType(e => e.HasConversion<string>().HasMaxLength(32));

        // E-mail is the login, so it is unique across tenants.
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
