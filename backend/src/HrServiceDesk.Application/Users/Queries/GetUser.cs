using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Users.Queries;

public sealed record GetUserQuery(Guid Id) : IRequest<Result<UserDetailsDto>>;

internal sealed class GetUserHandler(IAppDbContext db) : IRequestHandler<GetUserQuery, Result<UserDetailsDto>>
{
    public async Task<Result<UserDetailsDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == request.Id)
            .ProjectDetails(db)
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? UserErrors.NotFound : user;
    }
}
