using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Workflows;

namespace HrServiceDesk.Application.Workflows;

internal static class WorkflowMapping
{
    public static WorkflowDto ToDto(RequestType type, WorkflowDefinition? workflow) => new(
        type.Id,
        type.Name,
        type.IsConfidential,
        IsConfigured: workflow is not null,
        IsActive: workflow?.IsActive ?? false,
        Version: workflow?.Version ?? 0,
        workflow?.OrderedSteps.Select(s => new WorkflowStepDto(s.Order, s.Name, s.ApproverRole.ToString())).ToList() ?? []);
}
