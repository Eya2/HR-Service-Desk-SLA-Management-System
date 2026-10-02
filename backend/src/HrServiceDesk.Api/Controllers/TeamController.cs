using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Team;
using HrServiceDesk.Application.Team.Queries;
using HrServiceDesk.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>A manager's view of their direct reports' requests.</summary>
[Route("api/team")]
[Authorize(Policy = Policies.CanManageTeam)]
public sealed class TeamController : ApiControllerBase
{
    /// <summary>Non-confidential requests of the caller's direct reports.</summary>
    [HttpGet("tickets")]
    [ProducesResponseType<PagedResult<TicketSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketSummaryDto>>> Tickets(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListTeamTicketsQuery(status, page, pageSize), cancellationToken));

    /// <summary>Team size, open cases, recent volume, pending approvals and cases per status.</summary>
    [HttpGet("stats")]
    [ProducesResponseType<TeamStatsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamStatsDto>> Stats(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetTeamStatsQuery(), cancellationToken));
}
