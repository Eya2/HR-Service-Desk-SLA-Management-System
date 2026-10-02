namespace HrServiceDesk.Application.Workflows;

public sealed record WorkflowStepDto(int Order, string Name, string ApproverRole);

/// <summary>The approval chain of a request type; <see cref="Steps"/> is empty when none is configured.</summary>
public sealed record WorkflowDto(
    Guid RequestTypeId,
    string RequestTypeName,
    bool RequestTypeIsConfidential,
    Guid? ResponsibleTeamId,
    bool IsConfigured,
    bool IsActive,
    int Version,
    IReadOnlyList<WorkflowStepDto> Steps);
