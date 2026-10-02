using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Users;

/// <summary>A single-use password reset link. Only the token's hash is stored.</summary>
public sealed class PasswordResetToken : Entity, ITenantOwned
{
    private PasswordResetToken() { }

    public PasswordResetToken(User user, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(user);
        TenantId = user.TenantId;
        UserId = user.Id;
        TokenHash = tokenHash;
        CreatedAt = now;
        ExpiresAt = now + lifetime;
    }

    public Guid TenantId { get; set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;

    public void MarkUsed(DateTimeOffset now) => UsedAt ??= now;
}
