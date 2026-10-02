using HrServiceDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Auth;

internal static class SessionRevoker
{
    /// <summary>Revokes every active refresh token of a user (password change, deactivation). Caller saves.</summary>
    public static async Task RevokeAllAsync(IAppDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
            token.Revoke(now);
    }
}
