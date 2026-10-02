using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Domain.Workflows;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Workflows.Commands;

public sealed record SaveWorkflowStep(string Name, string ApproverRole);

/// <summary>Creates or replaces the approval chain of a request type. Cases already submitted keep their own copy.</summary>
public sealed record SaveWorkflowCommand(Guid RequestTypeId, bool IsActive, IReadOnlyList<SaveWorkflowStep> Steps) : IRequest<Result<WorkflowDto>>;

internal sealed class SaveWorkflowValidator : AbstractValidator<SaveWorkflowCommand>
{
    public SaveWorkflowValidator()
    {
        RuleFor(c => c.Steps).NotEmpty().Must(s => s.Count <= WorkflowDefinition.MaxSteps)
            .WithMessage($"A workflow has 1 to {WorkflowDefinition.MaxSteps} steps.");
        RuleForEach(c => c.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.Name).NotEmpty().MaximumLength(WorkflowDefinition.StepNameMaxLength);
            step.RuleFor(s => s.ApproverRole)
                .Must(r => Enum.TryParse<Role>(r, ignoreCase: false, out var role) && WorkflowDefinition.ApproverRoles.Contains(role))
                .WithMessage($"Approver must be one of: {string.Join(", ", WorkflowDefinition.ApproverRoles)}.");
        });
    }
}

internal sealed class SaveWorkflowHandler(IAppDbContext db) : IRequestHandler<SaveWorkflowCommand, Result<WorkflowDto>>
{
    public async Task<Result<WorkflowDto>> Handle(SaveWorkflowCommand request, CancellationToken cancellationToken)
    {
        var type = await db.RequestTypes.SingleOrDefaultAsync(t => t.Id == request.RequestTypeId, cancellationToken);
        if (type is null)
            return TicketErrors.RequestTypeNotFound;

        var steps = request.Steps.Select(s => (s.Name, Enum.Parse<Role>(s.ApproverRole))).ToList();
        var workflow = await db.WorkflowDefinitions.SingleOrDefaultAsync(w => w.RequestTypeId == type.Id, cancellationToken);
        if (workflow is null)
        {
            workflow = WorkflowDefinition.Create(type.Id, type.IsConfidential, steps);
            if (!request.IsActive)
                workflow.Update(type.IsConfidential, steps, isActive: false);
            db.WorkflowDefinitions.Add(workflow);
        }
        else
        {
            workflow.Update(type.IsConfidential, steps, request.IsActive);
        }

        await db.SaveChangesAsync(cancellationToken);
        return WorkflowMapping.ToDto(type, workflow);
    }
}
