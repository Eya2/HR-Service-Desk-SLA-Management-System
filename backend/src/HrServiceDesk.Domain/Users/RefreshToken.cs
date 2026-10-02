using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Users;

/// <summary>
/// A refresh token, stored as a hash only. Tokens are single-use: each refresh revokes the
/// presented token and issues a successor in the same <see cref="FamilyId"/>. Presenting a revoked
/// token again signals theft, and the whole family is revoked.
/// </summary>
public sealed class RefreshToken : Entity, ITenantOwned
{
    private RefreshToken() { }

    public Guid TenantId { get; set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Issue(User user, string tokenHash, DateTimeOffset now, TimeSpan lifetime, Guid? familyId = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = tokenHash,
            FamilyId = familyId ?? Guid.NewGuid(),
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
    }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
            return;
        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
