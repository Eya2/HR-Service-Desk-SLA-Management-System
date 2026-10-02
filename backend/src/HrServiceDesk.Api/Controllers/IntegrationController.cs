using HrServiceDesk.Api.Auth;
using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HrServiceDesk.Api.Controllers;

/// <summary>
/// Integration API for external systems (payroll, HRIS), authenticated with an <c>X-Api-Key</c> header.
/// Versioned in the path; confidential cases are never exposed.
/// </summary>
[Route("api/integration/v1")]
[Authorize(Policy = IntegrationPolicies.TicketsRead)]
[EnableRateLimiting(IntegrationPolicies.RateLimit)]
[ApiExplorerSettings(GroupName = "integration")]
public sealed class IntegrationController : ApiControllerBase
{
    public sealed record CommentRequest(string Body);

    public sealed record StatusRequest(string Status, string? Reason);

    /// <summary>Cases changed since <paramref name="updatedSince"/>, optionally by status or category, most recent first.</summary>
    [HttpGet("tickets")]
    [ProducesResponseType<PagedResult<IntegrationTicketSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IntegrationTicketSummaryDto>>> List(
        [FromQuery] DateTimeOffset? updatedSince, [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListIntegrationTicketsQuery(updatedSince, status, category, page, pageSize), cancellationToken));

    /// <summary>One case with its form answers and public conversation.</summary>
    [HttpGet("tickets/{id:guid}")]
    [ProducesResponseType<IntegrationTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntegrationTicketDto>> Get(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetIntegrationTicketQuery(id), cancellationToken));

    /// <summary>Posts a public reply as the key's service account.</summary>
    [HttpPost("tickets/{id:guid}/comments")]
    [Authorize(Policy = IntegrationPolicies.TicketsWrite)]
    [ProducesResponseType<IntegrationCommentDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IntegrationCommentDto>> Comment(Guid id, CommentRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new AddIntegrationCommentCommand(id, request.Body), cancellationToken));

    /// <summary>Moves the case like an HR agent would (e.g. InProgress, Resolved with a resolution message).</summary>
    [HttpPost("tickets/{id:guid}/status")]
    [Authorize(Policy = IntegrationPolicies.TicketsWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Status(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new ChangeIntegrationStatusCommand(id, request.Status, request.Reason), cancellationToken));
}
