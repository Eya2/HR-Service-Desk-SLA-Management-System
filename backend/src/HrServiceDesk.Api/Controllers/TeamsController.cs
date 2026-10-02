using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Teams;
using HrServiceDesk.Application.Teams.Commands;
using HrServiceDesk.Application.Teams.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>HR teams: HR staff read them (to assign cases); HR Admins maintain them.</summary>
[Route("api/teams")]
public sealed class TeamsController : ApiControllerBase
{
    public sealed record SaveTeamRequest(string Name, string Strategy, IReadOnlyList<Guid> MemberIds, bool IsConfidentialGroup = false);

    /// <summary>Teams with their members' active workload and the request types they handle.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanWorkTickets)]
    [ProducesResponseType<IReadOnlyList<TeamDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> List(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListTeamsQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<TeamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamDto>> Create(SaveTeamRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SaveTeamCommand(null, request.Name, request.Strategy, request.MemberIds ?? [], request.IsConfidentialGroup), cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<TeamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Update(Guid id, SaveTeamRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SaveTeamCommand(id, request.Name, request.Strategy, request.MemberIds ?? [], request.IsConfidentialGroup), cancellationToken));
}
