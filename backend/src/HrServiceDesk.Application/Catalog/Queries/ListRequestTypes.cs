using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Catalog.Queries;

/// <summary>The request catalog. Inactive types are listed only for HR Admins who ask for them.</summary>
public sealed record ListRequestTypesQuery(string? Search, string? Category, bool IncludeInactive = false)
    : IRequest<IReadOnlyList<RequestTypeSummaryDto>>;

internal sealed class ListRequestTypesHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListRequestTypesQuery, IReadOnlyList<RequestTypeSummaryDto>>
{
    public async Task<IReadOnlyList<RequestTypeSummaryDto>> Handle(ListRequestTypesQuery request, CancellationToken cancellationToken)
    {
        var query = db.RequestTypes.AsNoTracking();

        if (!(request.IncludeInactive && currentUser.IsInRole(Role.HrAdmin)))
            query = query.Where(t => t.IsActive);

        if (Enum.TryParse<RequestCategory>(request.Category, ignoreCase: false, out var category))
            query = query.Where(t => t.Category == category);

        var types = await query.OrderBy(t => t.Category).ThenBy(t => t.Name).ToListAsync(cancellationToken);

        // The catalog is small (tens of entries): searching in memory keeps it accent- and case-insensitive.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            types = types.Where(t => Matches(t.Name, term) || Matches(t.Description, term)).ToList();
        }

        return types.Select(t => new RequestTypeSummaryDto(
            t.Id, t.Name, t.Description, t.Category.ToString(), t.IsConfidential, t.IsActive, t.DefaultPriority.ToString())).ToList();
    }

    private static bool Matches(string text, string term) =>
        System.Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            text, term, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
}
