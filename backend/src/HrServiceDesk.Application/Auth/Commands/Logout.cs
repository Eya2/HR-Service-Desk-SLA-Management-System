using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Auth.Commands;

/// <summary>Ends the session the refresh token belongs to. Always succeeds, so it reveals nothing.</summary>
public sealed record LogoutCommand(string? RefreshToken) : IRequest<Result>;

internal sealed class LogoutHandler(IAppDbContext db, ITokenService tokens, TimeProvider clock)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result.Success();

        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
            return Result.Success();

        var now = clock.GetUtcNow();
        var family = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var t in family)
            t.Revoke(now);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
