using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Help centre: everyone reads published articles; HR Admins write them.</summary>
[Route("api/knowledge")]
public sealed class KnowledgeController : ApiControllerBase
{
    public sealed record SaveArticleRequest(string Title, string? Summary, string Body, string? Category, bool IsPublished);

    /// <summary>Published articles (all for HR Admins with includeUnpublished), searched with <paramref name="q"/>.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ArticleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ArticleSummaryDto>>> List([FromQuery] string? q, [FromQuery] bool includeUnpublished = false, CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListArticlesQuery(q, includeUnpublished), cancellationToken));

    /// <summary>Up to five articles matching what is being typed (used on the request form).</summary>
    [HttpGet("suggest")]
    [ProducesResponseType<IReadOnlyList<ArticleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ArticleSummaryDto>>> Suggest([FromQuery] string? text, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new SuggestArticlesQuery(text ?? string.Empty), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> Get(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetArticleQuery(id), cancellationToken));

    [HttpPost("{id:guid}/helpful")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Helpful(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new MarkArticleHelpfulCommand(id), cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ArticleDto>> Create(SaveArticleRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SaveArticleCommand(null, request.Title, request.Summary, request.Body, request.Category, request.IsPublished), cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ArticleDto>> Update(Guid id, SaveArticleRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SaveArticleCommand(id, request.Title, request.Summary, request.Body, request.Category, request.IsPublished), cancellationToken));
}
