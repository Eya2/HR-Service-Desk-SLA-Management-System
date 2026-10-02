using System.Security.Cryptography;
using System.Text;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Integration;

/// <summary>What an API key may do.</summary>
public static class ApiScopes
{
    public const string TicketsRead = "tickets:read";
    public const string TicketsWrite = "tickets:write";

    public static readonly IReadOnlyList<string> All = [TicketsRead, TicketsWrite];
}

/// <summary>
/// A key an external system (payroll, HRIS) uses to call the integration API on behalf of one organisation.
/// Only a SHA-256 hash is stored; the full key is shown once at creation. The public prefix finds the key
/// without scanning. Writes made with the key are attributed to its service account.
/// </summary>
public sealed class ApiKey : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;
    public const string KeyPrefix = "hrd_";
    private const int PrefixLength = 8;
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyz23456789";

    private ApiKey() { }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Public part of the key ("hrd_&lt;prefix&gt;_…"), used for lookup and display.</summary>
    public string Prefix { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public string[] Scopes { get; private set; } = [];

    /// <summary>Inactive user that writes made with this key are attributed to.</summary>
    public Guid ServiceUserId { get; private set; }

    public Guid? CreatedById { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsActive => RevokedAt is null;

    /// <summary>Creates a key; returns it with the full secret, which is never stored.</summary>
    public static (ApiKey Key, string Secret) Issue(string name, IEnumerable<string> scopes, Guid serviceUserId, Guid? createdById)
    {
        var prefix = RandomText(PrefixLength);
        var secret = $"{KeyPrefix}{prefix}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
        return (FromSecret(name, scopes, serviceUserId, createdById, secret), secret);
    }

    /// <summary>Registers a key whose secret is chosen outside (demo seed, key rotation from a vault).</summary>
    public static ApiKey FromSecret(string name, IEnumerable<string> scopes, Guid serviceUserId, Guid? createdById, string secret)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("api_key.invalid_name", $"Key names must be 1 to {NameMaxLength} characters.");
        var scopeList = (scopes ?? []).Distinct().ToArray();
        if (scopeList.Length == 0 || scopeList.Any(s => !ApiScopes.All.Contains(s)))
            throw new DomainException("api_key.invalid_scopes", $"Scopes must be among: {string.Join(", ", ApiScopes.All)}.");
        var prefix = PrefixOf(secret) ?? throw new DomainException("api_key.invalid_format", "The key does not have the expected format.");

        return new ApiKey
        {
            Name = name,
            Prefix = prefix,
            KeyHash = Hash(secret),
            Scopes = scopeList,
            ServiceUserId = serviceUserId,
            CreatedById = createdById,
        };
    }

    /// <summary>"hrd_abcd2345_…" → "abcd2345"; null when the text is not a key.</summary>
    public static string? PrefixOf(string? presented)
    {
        if (presented is null || !presented.StartsWith(KeyPrefix, StringComparison.Ordinal))
            return null;
        var rest = presented[KeyPrefix.Length..];
        var separator = rest.IndexOf('_', StringComparison.Ordinal);
        return separator == PrefixLength && rest.Length > PrefixLength + 16 ? rest[..PrefixLength] : null;
    }

    /// <summary>Constant-time comparison of the presented key with the stored hash.</summary>
    public bool Matches(string presented) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(presented)), Encoding.ASCII.GetBytes(KeyHash));

    public bool HasScope(string scope) => Scopes.Contains(scope);

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Records use at most once a minute, so busy integrations do not write on every call.</summary>
    public bool MarkUsed(DateTimeOffset now)
    {
        if (LastUsedAt is { } last && now - last < TimeSpan.FromMinutes(1))
            return false;
        LastUsedAt = now;
        return true;
    }

    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    private static string RandomText(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
