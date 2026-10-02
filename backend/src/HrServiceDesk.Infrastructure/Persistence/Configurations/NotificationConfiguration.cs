using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Escalations;
using HrServiceDesk.Domain.Notifications;
using HrServiceDesk.Domain.Teams;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(n => n.Title).HasMaxLength(Notification.TitleMaxLength).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength).IsRequired();
        builder.Ignore(n => n.IsRead);
        builder.HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(n => n.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(EscalationRule.NameMaxLength).IsRequired();
        builder.Property(r => r.Trigger).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<Team>().WithMany().HasForeignKey(r => r.TargetTeamId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<RequestType>().WithMany().HasForeignKey(r => r.RequestTypeId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EscalationExecutionConfiguration : IEntityTypeConfiguration<EscalationExecution>
{
    public void Configure(EntityTypeBuilder<EscalationExecution> builder)
    {
        builder.HasKey(x => x.Id);
        // The idempotency guarantee: a rule can fire only once per case, even if two monitors race.
        builder.HasIndex(x => new { x.TicketId, x.RuleId }).IsUnique();
        builder.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EscalationRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Cascade);
    }
}
