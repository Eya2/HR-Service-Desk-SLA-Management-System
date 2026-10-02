using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Workflows.Queries;

public sealed record GetWorkflowQuery(Guid RequestTypeId) : IRequest<Result<WorkflowDto>>;

internal sealed class GetWorkflowHandler(IAppDbContext db) : IRequestHandler<GetWorkflowQuery, Result<WorkflowDto>>
{
    public async Task<Result<WorkflowDto>> Handle(GetWorkflowQuery request, CancellationToken cancellationToken)
    {
        var type = await db.RequestTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.RequestTypeId, cancellationToken);
        if (type is null)
            return TicketErrors.RequestTypeNotFound;

        var workflow = await db.WorkflowDefinitions.AsNoTracking().SingleOrDefaultAsync(w => w.RequestTypeId == type.Id, cancellationToken);
        return WorkflowMapping.ToDto(type, workflow);
    }
}
