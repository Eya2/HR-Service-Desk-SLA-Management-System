using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HrServiceDesk.Application.Integration;

/// <summary>
/// Webhook signature: <c>X-HrDesk-Signature: t=&lt;unix seconds&gt;,v1=&lt;hex HMAC-SHA256(secret, "t.body")&gt;</c>.
/// Receivers recompute it with the shared secret and reject old timestamps to stop replays.
/// </summary>
public static class WebhookSignature
{
    public const string SignatureHeader = "X-HrDesk-Signature";
    public const string EventHeader = "X-HrDesk-Event";
    public const string DeliveryHeader = "X-HrDesk-Delivery";

    public static string Compute(string secret, long timestamp, string payload)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var signed = $"{timestamp.ToString(CultureInfo.InvariantCulture)}.{payload}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed));
        return $"t={timestamp.ToString(CultureInfo.InvariantCulture)},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    /// <summary>Checks a received header against the body; <paramref name="tolerance"/> bounds the timestamp's age.</summary>
    public static bool Verify(string secret, string? header, string payload, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrEmpty(header))
            return false;
        var parts = header.Split(',').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        if (!parts.TryGetValue("t", out var t) || !long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || !parts.TryGetValue("v1", out var received))
            return false;
        if ((now - DateTimeOffset.FromUnixTimeSeconds(timestamp)).Duration() > tolerance)
            return false;
        var expected = Compute(secret, timestamp, payload);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes($"t={t},v1={received}"));
    }

    /// <summary>A new random signing secret ("whsec_…").</summary>
    public static string NewSecret() => "whsec_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
