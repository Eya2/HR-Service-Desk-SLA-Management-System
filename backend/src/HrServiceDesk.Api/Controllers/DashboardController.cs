using System.Text;
using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>HR leadership dashboard and its CSV export.</summary>
[Route("api/dashboard")]
[Authorize(Policy = Policies.CanViewDashboards)]
public sealed class DashboardController : ApiControllerBase
{
    /// <summary>Indicators for [from, to) (default: the last 30 days), optionally for one team.</summary>
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? teamId, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetDashboardQuery(from, to, teamId), cancellationToken));

    /// <summary>The cases created in the period, one CSV row each (UTF-8 with BOM for spreadsheets).</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<ActionResult> Export(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? teamId, CancellationToken cancellationToken)
    {
        var csv = await Sender.Send(new ExportDashboardQuery(from, to, teamId), cancellationToken);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"hr-cases-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
