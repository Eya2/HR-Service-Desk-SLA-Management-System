using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Users.Queries;

/// <summary>Users of the caller's organisation, optionally filtered by text and role.</summary>
public sealed record ListUsersQuery(string? Search, string? Role, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<UserSummaryDto>>;

internal sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Role).Must(r => r is null || Enum.TryParse<Role>(r, out _)).WithMessage("Unknown role.");
    }
}

internal sealed class ListUsersHandler(IAppDbContext db) : IRequestHandler<ListUsersQuery, PagedResult<UserSummaryDto>>
{
    public async Task<PagedResult<UserSummaryDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            // Translated to SQL lower(...) LIKE; culture-aware .NET overloads are not translatable.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(u =>
                u.Email.Contains(term) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (request.Role is not null)
        {
            var role = Enum.Parse<Role>(request.Role);
            query = query.Where(u => u.Roles.Contains(role));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserSummaryDto(
                u.Id,
                u.Email,
                u.FirstName + " " + u.LastName,
                u.Roles.Select(r => r.ToString()).ToList(),
                u.IsActive,
                u.LastLoginAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSummaryDto>(items, request.Page, request.PageSize, total);
    }
}
