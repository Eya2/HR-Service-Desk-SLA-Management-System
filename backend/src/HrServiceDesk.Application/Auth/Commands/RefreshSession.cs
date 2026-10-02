using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Auth.Commands;

/// <summary>Exchanges a refresh token for a new session, rotating the refresh token.</summary>
public sealed record RefreshSessionCommand(string? RefreshToken) : IRequest<Result<AuthSession>>;

internal sealed class RefreshSessionHandler(
    IAppDbContext db,
    ITokenService tokens,
    SessionIssuer sessions,
    TimeProvider clock) : IRequestHandler<RefreshSessionCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return AuthErrors.InvalidRefreshToken;

        var now = clock.GetUtcNow();
        var hash = tokens.HashRefreshToken(request.RefreshToken);

        // No tenant is known yet: the cookie is the only credential.
        var token = await db.RefreshTokens.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
            return AuthErrors.InvalidRefreshToken;

        if (token.IsRevoked)
        {
            // A rotated token was presented again: assume it was stolen and end every session in the family.
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            return AuthErrors.RefreshTokenReused;
        }

        if (token.IsExpired(now))
            return AuthErrors.InvalidRefreshToken;

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == token.UserId, cancellationToken);
        var tenant = await db.Tenants.SingleAsync(t => t.Id == token.TenantId, cancellationToken);
        if (!user.IsActive || user.IsLockedOut(now) || !tenant.IsActive)
        {
            token.Revoke(now);
            await db.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        var session = sessions.Issue(user, tenant, token.FamilyId, token.IsPersistent, out var successor);
        token.Revoke(now, successor.Id);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var t in active)
            t.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);
    }
}
