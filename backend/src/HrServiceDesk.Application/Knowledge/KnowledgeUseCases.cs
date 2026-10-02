using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Knowledge;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Knowledge;

public sealed record ArticleSummaryDto(Guid Id, string Title, string Summary, string? Category, bool IsPublished, int ViewCount, int HelpfulCount);

public sealed record ArticleDto(Guid Id, string Title, string Summary, string Body, string? Category, bool IsPublished, int ViewCount, int HelpfulCount, DateTimeOffset? UpdatedAt);

/// <summary>Help-centre articles, searched when <see cref="Query"/> is given. Unpublished ones only for HR Admins who ask.</summary>
public sealed record ListArticlesQuery(string? Query, bool IncludeUnpublished = false) : IRequest<IReadOnlyList<ArticleSummaryDto>>;

/// <summary>Up to five published articles matching what the employee is typing (deflection).</summary>
public sealed record SuggestArticlesQuery(string Text) : IRequest<IReadOnlyList<ArticleSummaryDto>>;

/// <summary>One article; the read counts as a view unless the reader is an HR Admin.</summary>
public sealed record GetArticleQuery(Guid Id) : IRequest<Result<ArticleDto>>;

/// <summary>"This answered my question."</summary>
public sealed record MarkArticleHelpfulCommand(Guid Id) : IRequest<Result>;

public sealed record SaveArticleCommand(Guid? Id, string Title, string? Summary, string Body, string? Category, bool IsPublished) : IRequest<Result<ArticleDto>>;

internal sealed class SaveArticleValidator : AbstractValidator<SaveArticleCommand>
{
    public SaveArticleValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(KnowledgeArticle.TitleMaxLength);
        RuleFor(c => c.Summary).MaximumLength(KnowledgeArticle.SummaryMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(KnowledgeArticle.BodyMaxLength);
        RuleFor(c => c.Category).Must(c => c is null || Enum.TryParse<RequestCategory>(c, ignoreCase: false, out _)).WithMessage("Unknown category.");
    }
}

internal sealed class KnowledgeHandlers(IAppDbContext db, ICurrentUser currentUser, IKnowledgeSearch search)
    : IRequestHandler<ListArticlesQuery, IReadOnlyList<ArticleSummaryDto>>,
      IRequestHandler<SuggestArticlesQuery, IReadOnlyList<ArticleSummaryDto>>,
      IRequestHandler<GetArticleQuery, Result<ArticleDto>>,
      IRequestHandler<MarkArticleHelpfulCommand, Result>,
      IRequestHandler<SaveArticleCommand, Result<ArticleDto>>
{
    private static readonly Error NotFound = Error.NotFound("knowledge.not_found", "Article not found.");

    private bool SeesUnpublished => currentUser.IsInRole(Role.HrAdmin);

    public async Task<IReadOnlyList<ArticleSummaryDto>> Handle(ListArticlesQuery request, CancellationToken cancellationToken)
    {
        var publishedOnly = !(request.IncludeUnpublished && SeesUnpublished);
        if (!string.IsNullOrWhiteSpace(request.Query))
            return await RankedAsync(await search.SearchAsync(request.Query, 50, publishedOnly, cancellationToken), cancellationToken);

        var query = db.KnowledgeArticles.AsNoTracking();
        if (publishedOnly)
            query = query.Where(a => a.IsPublished);
        return (await query.OrderBy(a => a.Title).ToListAsync(cancellationToken)).Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<ArticleSummaryDto>> Handle(SuggestArticlesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length < 3)
            return [];
        return await RankedAsync(await search.SearchAsync(request.Text, 5, publishedOnly: true, cancellationToken), cancellationToken);
    }

    public async Task<Result<ArticleDto>> Handle(GetArticleQuery request, CancellationToken cancellationToken)
    {
        var article = await db.KnowledgeArticles.SingleOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (article is null || (!article.IsPublished && !SeesUnpublished))
            return NotFound;

        // Reads by HR Admins (editing, reviewing) would inflate the statistics: only the others count.
        if (!SeesUnpublished)
        {
            article.RecordView();
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToDto(article);
    }

    public async Task<Result> Handle(MarkArticleHelpfulCommand request, CancellationToken cancellationToken)
    {
        var article = await db.KnowledgeArticles.SingleOrDefaultAsync(a => a.Id == request.Id && a.IsPublished, cancellationToken);
        if (article is null)
            return NotFound;
        article.RecordHelpful();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ArticleDto>> Handle(SaveArticleCommand request, CancellationToken cancellationToken)
    {
        RequestCategory? category = request.Category is null ? null : Enum.Parse<RequestCategory>(request.Category);
        KnowledgeArticle article;
        if (request.Id is { } id)
        {
            var existing = await db.KnowledgeArticles.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (existing is null)
                return NotFound;
            article = existing;
            article.Update(request.Title, request.Summary ?? string.Empty, request.Body, category);
            article.SetPublished(request.IsPublished);
        }
        else
        {
            article = KnowledgeArticle.Create(request.Title, request.Summary ?? string.Empty, request.Body, category, request.IsPublished);
            db.KnowledgeArticles.Add(article);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(article);
    }

    private async Task<IReadOnlyList<ArticleSummaryDto>> RankedAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var articles = await db.KnowledgeArticles.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return ids.Where(articles.ContainsKey).Select(id => ToSummary(articles[id])).ToList();
    }

    private static ArticleSummaryDto ToSummary(KnowledgeArticle a) =>
        new(a.Id, a.Title, a.Summary, a.Category?.ToString(), a.IsPublished, a.ViewCount, a.HelpfulCount);

    private static ArticleDto ToDto(KnowledgeArticle a) =>
        new(a.Id, a.Title, a.Summary, a.Body, a.Category?.ToString(), a.IsPublished, a.ViewCount, a.HelpfulCount, a.UpdatedAt);
}
