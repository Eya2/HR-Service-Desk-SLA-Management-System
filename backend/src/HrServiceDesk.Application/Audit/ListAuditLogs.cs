using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Domain.Audit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Audit;

public sealed record AuditLogDto(Guid Id, DateTimeOffset OccurredAt, Guid? UserId, string UserName, string Action, string EntityType, Guid? EntityId, string Summary);

/// <summary>The organisation's audit log (HR Admins and auditors), newest first.</summary>
public sealed record ListAuditLogsQuery(DateTimeOffset? From, DateTimeOffset? To, string? Action, Guid? UserId, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<AuditLogDto>>;

internal sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        RuleFor(q => q.Action).Must(a => a is null || Enum.TryParse<AuditAction>(a, out _)).WithMessage("Unknown action.");
    }
}

internal sealed class ListAuditLogsHandler(IAppDbContext db) : IRequestHandler<ListAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (request.From is { } from)
            query = query.Where(l => l.OccurredAt >= from);
        if (request.To is { } to)
            query = query.Where(l => l.OccurredAt < to);
        if (request.Action is not null)
        {
            var action = Enum.Parse<AuditAction>(request.Action);
            query = query.Where(l => l.Action == action);
        }

        if (request.UserId is { } userId)
            query = query.Where(l => l.UserId == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await (
            from l in query.OrderByDescending(l => l.OccurredAt).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            from u in db.Users.Where(u => u.Id == l.UserId).DefaultIfEmpty()
            select new AuditLogDto(
                l.Id, l.OccurredAt, l.UserId, u != null ? u.FirstName + " " + u.LastName : "System",
                l.Action.ToString(), l.EntityType, l.EntityId, l.Summary)).ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items.OrderByDescending(i => i.OccurredAt).ToList(), request.Page, request.PageSize, total);
    }
}
