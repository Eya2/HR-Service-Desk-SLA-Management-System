using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Reference).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(Ticket.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Ticket.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.FormData).HasColumnType("jsonb").IsRequired();
        builder.Ignore(t => t.IsFinal);

        builder.HasIndex(t => new { t.TenantId, t.Reference }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.AssigneeId });
        builder.HasIndex(t => new { t.TenantId, t.RequesterId });

        builder.HasOne<RequestType>().WithMany().HasForeignKey(t => t.RequestTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Comments).WithOne().HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Attachments).WithOne().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Events).WithOne().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Approvals).WithOne().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(t => t.CurrentApproval);
    }
}

internal sealed class TicketApprovalConfiguration : IEntityTypeConfiguration<TicketApproval>
{
    public void Configure(EntityTypeBuilder<TicketApproval> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.StepName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.ApproverRole).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Decision).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Comment).HasMaxLength(TicketApproval.CommentMaxLength);
        builder.HasIndex(a => new { a.TicketId, a.StepOrder }).IsUnique();
        builder.HasIndex(a => new { a.ApproverUserId, a.Decision });
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.DecidedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketEventConfiguration : IEntityTypeConfiguration<TicketEvent>
{
    public void Configure(EntityTypeBuilder<TicketEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Data).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.TicketId, e.OccurredAt });
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Body).HasMaxLength(Comment.BodyMaxLength).IsRequired();
        builder.HasIndex(c => c.TicketId);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).HasMaxLength(Attachment.FileNameMaxLength).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(100).IsRequired();
        builder.Property(a => a.FieldKey).HasMaxLength(50);
        builder.HasIndex(a => a.TicketId);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReferenceCounterConfiguration : IEntityTypeConfiguration<ReferenceCounter>
{
    public void Configure(EntityTypeBuilder<ReferenceCounter> builder) => builder.HasKey(c => new { c.TenantId, c.Year });
}
