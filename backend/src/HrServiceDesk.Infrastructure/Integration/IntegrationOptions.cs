using System.Security.Cryptography;
using System.Text;
using HrServiceDesk.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Infrastructure.Integration;

/// <summary>Bound from <c>Integration</c>.</summary>
public sealed class IntegrationOptions
{
    public const string SectionName = "Integration";

    /// <summary>Encrypts webhook signing secrets at rest. Supplied through the environment, never committed.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Accept plain-http webhook URLs (local development, the demo network).</summary>
    public bool AllowInsecureWebhookUrls { get; set; }

    /// <summary>Allow webhooks to private and loopback addresses. Off in production to prevent SSRF.</summary>
    public bool AllowPrivateNetworks { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}

internal sealed class IntegrationSettings(IOptions<IntegrationOptions> options) : IIntegrationSettings
{
    public bool AllowInsecureWebhookUrls => options.Value.AllowInsecureWebhookUrls;
}

/// <summary>AES-256-GCM with a key derived from <see cref="IntegrationOptions.SecretKey"/>; stored as "v1:" + base64(nonce|cipher|tag).</summary>
internal sealed class AesSecretProtector(IOptions<IntegrationOptions> options) : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private byte[] Key
    {
        get
        {
            var secret = options.Value.SecretKey;
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 16)
                throw new InvalidOperationException("Integration:SecretKey must be configured (at least 16 characters).");
            return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        }
    }

    public string Protect(string secret)
    {
        var plain = Encoding.UTF8.GetBytes(secret);
        var output = new byte[NonceSize + plain.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(Key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize, plain.Length), output.AsSpan(NonceSize + plain.Length, TagSize));
        return "v1:" + Convert.ToBase64String(output);
    }

    public string Unprotect(string protectedSecret)
    {
        if (!protectedSecret.StartsWith("v1:", StringComparison.Ordinal))
            throw new CryptographicException("Unknown secret format.");
        var data = Convert.FromBase64String(protectedSecret[3..]);
        var plain = new byte[data.Length - NonceSize - TagSize];
        using var aes = new AesGcm(Key, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize, plain.Length), data.AsSpan(NonceSize + plain.Length, TagSize), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
