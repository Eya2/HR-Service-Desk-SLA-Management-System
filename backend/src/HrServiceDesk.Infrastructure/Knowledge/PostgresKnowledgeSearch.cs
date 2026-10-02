using System.Text;
using System.Text.RegularExpressions;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Infrastructure.Knowledge;

/// <summary>
/// PostgreSQL full-text search: accent-insensitive (unaccent), prefix matching so partial words already
/// match while typing, any word may match, and the title weighs more than the summary and the body.
/// The tenant is filtered explicitly because raw SQL bypasses the global query filters.
/// </summary>
internal sealed partial class PostgresKnowledgeSearch(AppDbContext db, ITenantContext tenantContext) : IKnowledgeSearch
{
    private sealed record Hit(Guid Id, float Rank);

    public async Task<IReadOnlyList<Guid>> SearchAsync(string text, int limit, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = ToTsQuery(text);
        if (query is null || tenantContext.TenantId is not { } tenantId)
            return [];

        var hits = await db.Database.SqlQuery<Hit>($"""
            SELECT id AS "Id",
                   ts_rank(
                       setweight(to_tsvector('simple', unaccent(title)), 'A') ||
                       setweight(to_tsvector('simple', unaccent(summary)), 'B') ||
                       setweight(to_tsvector('simple', unaccent(body)), 'C'),
                       to_tsquery('simple', unaccent({query}))) AS "Rank"
            FROM knowledge_articles
            WHERE tenant_id = {tenantId}
              AND ({publishedOnly} = false OR is_published)
              AND (setweight(to_tsvector('simple', unaccent(title)), 'A') ||
                   setweight(to_tsvector('simple', unaccent(summary)), 'B') ||
                   setweight(to_tsvector('simple', unaccent(body)), 'C')) @@ to_tsquery('simple', unaccent({query}))
            ORDER BY 2 DESC, 1
            LIMIT {limit}
            """).ToListAsync(cancellationToken);

        return hits.Select(h => h.Id).ToList();
    }

    /// <summary>"pay slip erreur" → "pay:* | slip:* | erreur:*". Only letters and digits reach the query; very short words are ignored.</summary>
    internal static string? ToTsQuery(string text)
    {
        var words = WordPattern().Matches(text ?? string.Empty)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => w.Length >= 3)
            .Distinct()
            .Take(10)
            .ToList();
        if (words.Count == 0)
            return null;

        var builder = new StringBuilder();
        foreach (var word in words)
        {
            if (builder.Length > 0)
                builder.Append(" | ");
            builder.Append(word).Append(":*");
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
