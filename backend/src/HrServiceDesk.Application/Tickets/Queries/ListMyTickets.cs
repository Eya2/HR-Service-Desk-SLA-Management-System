using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Queries;

/// <summary>The caller's own requests, newest first.</summary>
public sealed record ListMyTicketsQuery(string? Status, string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<TicketSummaryDto>>;

internal sealed class ListMyTicketsValidator : AbstractValidator<ListMyTicketsQuery>
{
    public ListMyTicketsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Status).Must(s => s is null || Enum.TryParse<TicketStatus>(s, out _)).WithMessage("Unknown status.");
    }
}

internal sealed class ListMyTicketsHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListMyTicketsQuery, PagedResult<TicketSummaryDto>>
{
    public async Task<PagedResult<TicketSummaryDto>> Handle(ListMyTicketsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var query = db.Tickets.AsNoTracking().Where(t => t.RequesterId == userId).Search(request.Search);
        if (request.Status is not null)
        {
            var status = Enum.Parse<TicketStatus>(request.Status);
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToSummaries(db)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketSummaryDto>(items, request.Page, request.PageSize, total);
    }
}
