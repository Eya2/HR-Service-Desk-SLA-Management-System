using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Application.Tickets.Queries;

internal static class TicketListing
{
    public static IQueryable<Ticket> Search(this IQueryable<Ticket> tickets, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return tickets;
        var term = search.Trim().ToLowerInvariant();
        // Translated to SQL lower(...) LIKE; culture-aware .NET overloads are not translatable.
#pragma warning disable CA1304, CA1311, CA1862
        return tickets.Where(t => t.Reference.ToLower().Contains(term) || t.Title.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
    }

    public static IQueryable<TicketSummaryDto> ToSummaries(this IQueryable<Ticket> tickets, IAppDbContext db) =>
        from t in tickets
        join type in db.RequestTypes on t.RequestTypeId equals type.Id
        join requester in db.Users on t.RequesterId equals requester.Id
        select new TicketSummaryDto(
            t.Id,
            t.Reference,
            t.Title,
            type.Id,
            type.Name,
            type.Category.ToString(),
            t.Status.ToString(),
            t.Priority.ToString(),
            t.IsConfidential,
            requester.Id,
            requester.FirstName + " " + requester.LastName,
            t.CreatedAt,
            t.UpdatedAt);
}
