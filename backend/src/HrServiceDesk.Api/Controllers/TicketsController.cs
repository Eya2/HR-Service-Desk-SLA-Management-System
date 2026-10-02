using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using FluentValidation.Results;
using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Application.Tickets.Commands;
using HrServiceDesk.Application.Tickets.Files;
using HrServiceDesk.Application.Tickets.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>HR cases: submission, follow-up, comments and attachments.</summary>
[Route("api/tickets")]
public sealed class TicketsController : ApiControllerBase
{
    // 10 files of 10 MB plus the JSON payload.
    private const long MaxRequestBytes = 110L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>JSON carried in the <c>payload</c> part of a submission.</summary>
    public sealed record SubmitPayload(Guid RequestTypeId, string Title, string? Description, JsonObject? Values);

    public sealed record UpdateTicketRequest(string Title, string? Description, string? Priority);

    public sealed record AddCommentRequest(string Body, bool IsInternal);

    public sealed record ChangeStatusRequest(string Status, string? Reason);

    public sealed record AssignRequest(Guid? AssigneeId);

    public sealed record MoveToTeamRequest(Guid TeamId);

    public sealed record RatingRequest(int Score, string? Comment);

    /// <summary>
    /// Submits a request. Send <c>multipart/form-data</c> with a <c>payload</c> part (JSON: requestTypeId,
    /// title, description, values) and one file part per uploaded document, named after its form field key.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<TicketCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketCreatedDto>> Submit([FromForm] string payload, CancellationToken cancellationToken)
    {
        var body = ParsePayload(payload);
        var files = Request.Form.Files.Select(ToUploadedFile).ToList();
        var result = await Sender.Send(
            new SubmitTicketCommand(body.RequestTypeId, body.Title, body.Description, body.Values, files), cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value) : Problem(result.Error!);
    }

    /// <summary>The caller's own requests.</summary>
    [HttpGet("mine")]
    [ProducesResponseType<PagedResult<TicketSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketSummaryDto>>> Mine(
        [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListMyTicketsQuery(status, search, page, pageSize), cancellationToken));

    /// <summary>
    /// Cases visible to HR staff or auditors. <paramref name="scope"/>: All, Mine (assigned to me), MyTeams, Unassigned;
    /// <paramref name="activeOnly"/> hides resolved and closed cases.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewAllTickets)]
    [ProducesResponseType<PagedResult<TicketSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketSummaryDto>>> List(
        [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] Guid? requestTypeId, [FromQuery] string? search,
        [FromQuery] TicketScope scope = TicketScope.All, [FromQuery] Guid? teamId = null, [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(
            new ListTicketsQuery(status, priority, requestTypeId, search, page, pageSize, scope, teamId, activeOnly), cancellationToken));

    /// <summary>The caller takes the case. 409 if someone else already holds it or took it at the same moment.</summary>
    [HttpPost("{id:guid}/claim")]
    [Authorize(Policy = Policies.CanWorkTickets)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Claim(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new ClaimTicketCommand(id), cancellationToken));

    /// <summary>Assigns the case to an HR staff member, or back to the team queue when <c>assigneeId</c> is null.</summary>
    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = Policies.CanWorkTickets)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Assign(Guid id, AssignRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new AssignTicketCommand(id, request.AssigneeId), cancellationToken));

    /// <summary>Hands the case to another team, which assigns it according to its strategy.</summary>
    [HttpPut("{id:guid}/team")]
    [Authorize(Policy = Policies.CanWorkTickets)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MoveToTeam(Guid id, MoveToTeamRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new MoveTicketToTeamCommand(id, request.TeamId), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailsDto>> Get(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetTicketQuery(id), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new UpdateTicketCommand(id, request.Title, request.Description, request.Priority), cancellationToken));

    /// <summary>Moves the case to another status, if the status machine allows it for the caller.</summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new ChangeTicketStatusCommand(id, request.Status, request.Reason), cancellationToken));

    /// <summary>The requester rates the closed case (1 to 5), once.</summary>
    [HttpPost("{id:guid}/satisfaction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Rate(Guid id, RatingRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new RateTicketCommand(id, request.Score, request.Comment), cancellationToken));

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CommentDto>> AddComment(Guid id, AddCommentRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new AddCommentCommand(id, request.Body, request.IsInternal), cancellationToken));

    /// <summary>Adds documents to a case (multipart/form-data, any part names).</summary>
    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<IReadOnlyList<AttachmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> AddAttachments(Guid id, IFormFileCollection files, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new AddAttachmentsCommand(id, files.Select(ToUploadedFile).ToList()), cancellationToken));

    /// <summary>Downloads an attachment.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Download(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetAttachmentQuery(id, attachmentId), cancellationToken);
        if (!result.IsSuccess)
            return Problem(result.Error!);

        // Always a download (Content-Disposition: attachment), never rendered inline by the browser.
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private static UploadedFile ToUploadedFile(IFormFile file) => new(file.Name, file.FileName, file.Length, file.OpenReadStream);

    private static SubmitPayload ParsePayload(string payload)
    {
        try
        {
            if (JsonSerializer.Deserialize<SubmitPayload>(payload, JsonOptions) is { } body)
                return body;
        }
        catch (JsonException)
        {
        }

        throw new ValidationException([new ValidationFailure("payload", "The payload part must be a JSON object.")]);
    }
}
