using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Domain.Workflows;

/// <summary>One approval step: who must approve, in order.</summary>
public sealed record WorkflowStep(int Order, string Name, Role ApproverRole);

/// <summary>
/// The approval chain of a request type. Cases copy the steps when they are submitted, so editing a
/// definition (which bumps <see cref="Version"/>) never changes cases already in flight.
/// </summary>
public sealed class WorkflowDefinition : Entity, ITenantOwned, IAuditable
{
    public const int MaxSteps = 5;
    public const int StepNameMaxLength = 100;

    /// <summary>Roles that can be asked to approve. "Manager" means the requester's own manager.</summary>
    public static readonly IReadOnlySet<Role> ApproverRoles =
        new HashSet<Role> { Role.Manager, Role.HrOfficer, Role.PayrollSpecialist, Role.HrAdmin };

    private readonly List<WorkflowStep> _steps = [];

    private WorkflowDefinition() { }

    public Guid TenantId { get; set; }

    public Guid RequestTypeId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int Version { get; private set; } = 1;

    public IReadOnlyCollection<WorkflowStep> Steps => _steps.AsReadOnly();

    public IReadOnlyList<WorkflowStep> OrderedSteps => _steps.OrderBy(s => s.Order).ToList();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static WorkflowDefinition Create(Guid requestTypeId, bool requestTypeIsConfidential, IEnumerable<(string Name, Role Role)> steps)
    {
        var definition = new WorkflowDefinition { RequestTypeId = requestTypeId };
        definition.ApplySteps(requestTypeIsConfidential, steps);
        return definition;
    }

    public void Update(bool requestTypeIsConfidential, IEnumerable<(string Name, Role Role)> steps, bool isActive)
    {
        ApplySteps(requestTypeIsConfidential, steps);
        IsActive = isActive;
        Version++;
    }

    private void ApplySteps(bool confidential, IEnumerable<(string Name, Role Role)> steps)
    {
        var list = (steps ?? []).ToList();
        if (list.Count is 0 or > MaxSteps)
            throw new DomainException("workflow.invalid_steps", $"A workflow needs 1 to {MaxSteps} steps.");

        var built = new List<WorkflowStep>();
        foreach (var (name, role) in list)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length is 0 or > StepNameMaxLength)
                throw new DomainException("workflow.invalid_step_name", $"Step names must be 1 to {StepNameMaxLength} characters.");
            if (!ApproverRoles.Contains(role))
                throw new DomainException("workflow.invalid_approver", $"{role} cannot approve requests.");
            // Until the restricted HR group exists, only HR Admins may see confidential cases.
            if (confidential && role != Role.HrAdmin)
                throw new DomainException("workflow.confidential_approver", "Confidential requests can only be approved by HR Admins.");
            built.Add(new WorkflowStep(built.Count + 1, trimmed, role));
        }

        _steps.Clear();
        _steps.AddRange(built);
    }
}
