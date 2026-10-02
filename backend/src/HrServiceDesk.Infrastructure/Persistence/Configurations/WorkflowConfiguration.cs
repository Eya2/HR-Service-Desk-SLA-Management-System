using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrServiceDesk.Infrastructure.Persistence.Configurations;

internal sealed class WorkflowConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.HasKey(w => w.Id);
        builder.HasIndex(w => new { w.TenantId, w.RequestTypeId }).IsUnique();
        builder.HasOne<RequestType>().WithMany().HasForeignKey(w => w.RequestTypeId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(w => w.OrderedSteps);

        // Steps are replaced as a whole on edit, so they get a surrogate key rather than (workflow, order).
        builder.OwnsMany(w => w.Steps, step =>
        {
            step.ToTable("workflow_steps");
            step.WithOwner().HasForeignKey("WorkflowDefinitionId");
            step.Property<int>("Id").UseIdentityAlwaysColumn();
            step.HasKey("Id");
            step.Property(s => s.Name).HasMaxLength(WorkflowDefinition.StepNameMaxLength).IsRequired();
            step.Property(s => s.ApproverRole).HasConversion<string>().HasMaxLength(32);
        });
    }
}
