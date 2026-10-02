using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Catalog;
using HrServiceDesk.Application.Catalog.Commands;
using HrServiceDesk.Application.Catalog.Queries;
using HrServiceDesk.Application.Teams.Commands;
using HrServiceDesk.Domain.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>The HR request catalog. Everyone can browse it; HR Admins maintain it.</summary>
[Route("api/request-types")]
public sealed class RequestTypesController : ApiControllerBase
{
    public sealed record SaveRequestTypeRequest(
        string Name, string? Description, string Category, bool IsConfidential, string DefaultPriority, IReadOnlyList<FormField> Fields);

    public sealed record SetActiveRequest(bool IsActive);

    public sealed record SetTeamRequest(Guid? TeamId);

    /// <summary>Lists the catalog, with optional text search and category filter.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequestTypeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestTypeSummaryDto>>> List(
        [FromQuery] string? search, [FromQuery] string? category, [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListRequestTypesQuery(search, category, includeInactive), cancellationToken));

    /// <summary>A request type with its form definition.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RequestTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestTypeDto>> Get(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetRequestTypeQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<RequestTypeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RequestTypeDto>> Create(SaveRequestTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(ToCommand(null, request), cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value) : Problem(result.Error!);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<RequestTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RequestTypeDto>> Update(Guid id, SaveRequestTypeRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(ToCommand(id, request), cancellationToken));

    /// <summary>Removes a type from the catalog or restores it. Existing cases keep working.</summary>
    [HttpPut("{id:guid}/active")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SetActive(Guid id, SetActiveRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SetRequestTypeActiveCommand(id, request.IsActive), cancellationToken));

    /// <summary>Chooses the team that handles cases of this type (null: none).</summary>
    [HttpPut("{id:guid}/team")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetTeam(Guid id, SetTeamRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SetResponsibleTeamCommand(id, request.TeamId), cancellationToken));

    private static SaveRequestTypeCommand ToCommand(Guid? id, SaveRequestTypeRequest r) =>
        new(id, r.Name, r.Description, r.Category, r.IsConfidential, r.DefaultPriority, r.Fields ?? []);
}
