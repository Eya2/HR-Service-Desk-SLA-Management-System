using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Auth.Queries;

public sealed record GetCurrentUserQuery : IRequest<Result<UserProfileDto>>;

internal sealed class GetCurrentUserHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return AuthErrors.NotAuthenticated;

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return AuthErrors.NotAuthenticated;

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == user.TenantId, cancellationToken);
        return SessionIssuer.ToProfile(user, tenant);
    }
}
