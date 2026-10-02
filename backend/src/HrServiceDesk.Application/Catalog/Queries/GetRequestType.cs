using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Catalog.Queries;

/// <summary>A request type with its form definition, used to render the submission form.</summary>
public sealed record GetRequestTypeQuery(Guid Id) : IRequest<Result<RequestTypeDto>>;

internal sealed class GetRequestTypeHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetRequestTypeQuery, Result<RequestTypeDto>>
{
    public async Task<Result<RequestTypeDto>> Handle(GetRequestTypeQuery request, CancellationToken cancellationToken)
    {
        var type = await db.RequestTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (type is null || (!type.IsActive && !currentUser.IsInRole(Role.HrAdmin)))
            return CatalogRules.NotFound;

        return CatalogRules.ToDto(type);
    }
}
