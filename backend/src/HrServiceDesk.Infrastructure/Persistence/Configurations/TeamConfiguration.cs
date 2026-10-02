using HrServiceDesk.Domain.Teams;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.Property(t => t.Strategy).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
        builder.Ignore(t => t.OrderedMemberIds);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.LastAssignedUserId).OnDelete(DeleteBehavior.SetNull);

        builder.OwnsMany(t => t.Members, member =>
        {
            member.ToTable("team_members");
            member.WithOwner().HasForeignKey("TeamId");
            member.HasKey("TeamId", nameof(TeamMember.UserId));
            member.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
