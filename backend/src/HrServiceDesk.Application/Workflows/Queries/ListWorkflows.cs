using HrServiceDesk.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Workflows.Queries;

/// <summary>Every request type with its approval chain (if any), for the admin workflow editor.</summary>
public sealed record ListWorkflowsQuery : IRequest<IReadOnlyList<WorkflowDto>>;

internal sealed class ListWorkflowsHandler(IAppDbContext db) : IRequestHandler<ListWorkflowsQuery, IReadOnlyList<WorkflowDto>>
{
    public async Task<IReadOnlyList<WorkflowDto>> Handle(ListWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var types = await db.RequestTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var workflows = await db.WorkflowDefinitions.AsNoTracking().ToDictionaryAsync(w => w.RequestTypeId, cancellationToken);

        return types.Select(t => WorkflowMapping.ToDto(t, workflows.GetValueOrDefault(t.Id))).ToList();
    }
}
