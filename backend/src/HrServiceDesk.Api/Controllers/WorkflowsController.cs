using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Workflows;
using HrServiceDesk.Application.Workflows.Commands;
using HrServiceDesk.Application.Workflows.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Approval chains per request type (HR Admin).</summary>
[Route("api/workflows")]
[Authorize(Policy = Policies.CanAdministerTenant)]
public sealed class WorkflowsController : ApiControllerBase
{
    public sealed record SaveWorkflowRequest(bool IsActive, IReadOnlyList<SaveWorkflowStep> Steps);

    /// <summary>Every request type with its approval chain, if any.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkflowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowDto>>> List(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListWorkflowsQuery(), cancellationToken));

    [HttpGet("{requestTypeId:guid}")]
    [ProducesResponseType<WorkflowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowDto>> Get(Guid requestTypeId, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetWorkflowQuery(requestTypeId), cancellationToken));

    /// <summary>Creates or replaces the approval chain of a request type. In-flight cases keep their own copy.</summary>
    [HttpPut("{requestTypeId:guid}")]
    [ProducesResponseType<WorkflowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkflowDto>> Save(Guid requestTypeId, SaveWorkflowRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SaveWorkflowCommand(requestTypeId, request.IsActive, request.Steps ?? []), cancellationToken));
}
