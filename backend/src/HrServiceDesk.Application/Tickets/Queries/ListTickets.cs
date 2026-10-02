using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Queries;

/// <summary>All cases the caller may see (HR staff and auditors), with filters.</summary>
public sealed record ListTicketsQuery(
    string? Status, string? Priority, Guid? RequestTypeId, string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<TicketSummaryDto>>;

internal sealed class ListTicketsValidator : AbstractValidator<ListTicketsQuery>
{
    public ListTicketsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Status).Must(s => s is null || Enum.TryParse<TicketStatus>(s, out _)).WithMessage("Unknown status.");
        RuleFor(q => q.Priority).Must(p => p is null || Enum.TryParse<TicketPriority>(p, out _)).WithMessage("Unknown priority.");
    }
}

internal sealed class ListTicketsHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListTicketsQuery, PagedResult<TicketSummaryDto>>
{
    public async Task<PagedResult<TicketSummaryDto>> Handle(ListTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Tickets.AsNoTracking().VisibleTo(db, currentUser).Search(request.Search);
        if (request.Status is not null)
        {
            var status = Enum.Parse<TicketStatus>(request.Status);
            query = query.Where(t => t.Status == status);
        }

        if (request.Priority is not null)
        {
            var priority = Enum.Parse<TicketPriority>(request.Priority);
            query = query.Where(t => t.Priority == priority);
        }

        if (request.RequestTypeId is { } typeId)
            query = query.Where(t => t.RequestTypeId == typeId);

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
