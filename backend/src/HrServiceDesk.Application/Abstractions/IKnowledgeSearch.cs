namespace HrServiceDesk.Application.Abstractions;

/// <summary>Full-text search over the current organisation's articles, best match first.</summary>
public interface IKnowledgeSearch
{
    Task<IReadOnlyList<Guid>> SearchAsync(string text, int limit, bool publishedOnly, CancellationToken cancellationToken);
}
