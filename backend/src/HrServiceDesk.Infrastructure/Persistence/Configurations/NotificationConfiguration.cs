using HrServiceDesk.Domain.Audit;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Escalations;
using HrServiceDesk.Domain.Knowledge;
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

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<string>().HasMaxLength(48);
        builder.Property(l => l.EntityType).HasMaxLength(32).IsRequired();
        builder.Property(l => l.Summary).HasMaxLength(AuditLog.SummaryMaxLength).IsRequired();
        builder.HasIndex(l => new { l.TenantId, l.OccurredAt });
        builder.HasIndex(l => new { l.TenantId, l.UserId });
        // No foreign key to users: the log must outlive the accounts it mentions.
    }
}

internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(KnowledgeArticle.TitleMaxLength).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(KnowledgeArticle.SummaryMaxLength).IsRequired();
        builder.Property(a => a.Body).HasMaxLength(KnowledgeArticle.BodyMaxLength).IsRequired();
        builder.Property(a => a.Category).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(a => new { a.TenantId, a.IsPublished });
    }
}

internal sealed class SatisfactionRatingConfiguration : IEntityTypeConfiguration<SatisfactionRating>
{
    public void Configure(EntityTypeBuilder<SatisfactionRating> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Comment).HasMaxLength(SatisfactionRating.CommentMaxLength);
        builder.HasIndex(r => r.TicketId).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.CreatedAt });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(r => r.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}
