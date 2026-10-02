using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Knowledge;

/// <summary>
/// A help-centre article. Published articles are suggested to employees while they describe a request,
/// so simple questions are answered without creating a case (deflection).
/// </summary>
public sealed class KnowledgeArticle : Entity, ITenantOwned, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int SummaryMaxLength = 300;
    public const int BodyMaxLength = 8000;

    private KnowledgeArticle() { }

    public Guid TenantId { get; set; }

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    /// <summary>Plain text; blank lines separate paragraphs.</summary>
    public string Body { get; private set; } = string.Empty;

    public RequestCategory? Category { get; private set; }

    public bool IsPublished { get; private set; }

    public int ViewCount { get; private set; }

    /// <summary>How many times an employee said the article answered their question.</summary>
    public int HelpfulCount { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static KnowledgeArticle Create(string title, string summary, string body, RequestCategory? category, bool publish)
    {
        var article = new KnowledgeArticle();
        article.Update(title, summary, body, category);
        article.IsPublished = publish;
        return article;
    }

    public void Update(string title, string summary, string body, RequestCategory? category)
    {
        title = (title ?? string.Empty).Trim();
        summary = (summary ?? string.Empty).Trim();
        body = (body ?? string.Empty).Trim();
        if (title.Length is 0 or > TitleMaxLength)
            throw new DomainException("knowledge.invalid_title", $"The title must be 1 to {TitleMaxLength} characters.");
        if (summary.Length > SummaryMaxLength)
            throw new DomainException("knowledge.invalid_summary", $"The summary must be at most {SummaryMaxLength} characters.");
        if (body.Length is 0 or > BodyMaxLength)
            throw new DomainException("knowledge.invalid_body", $"The content must be 1 to {BodyMaxLength} characters.");

        Title = title;
        Summary = summary;
        Body = body;
        Category = category;
    }

    public void SetPublished(bool isPublished) => IsPublished = isPublished;

    public void RecordView() => ViewCount++;

    public void RecordHelpful() => HelpfulCount++;
}
