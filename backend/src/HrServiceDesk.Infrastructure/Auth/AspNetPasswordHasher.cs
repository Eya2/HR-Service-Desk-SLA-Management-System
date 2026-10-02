using HrServiceDesk.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace HrServiceDesk.Infrastructure.Auth;

/// <summary>Wraps ASP.NET Core Identity's PBKDF2 hasher (HMAC-SHA512, 100k iterations, per-password salt).</summary>
internal sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly object HashContext = new();
    private readonly PasswordHasher<object> _inner = new();

    public string Hash(string password) => _inner.HashPassword(HashContext, password);

    public PasswordCheck Verify(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(HashContext, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
}
