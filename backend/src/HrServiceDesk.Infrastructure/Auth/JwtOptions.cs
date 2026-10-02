using System.Text;

namespace HrServiceDesk.Infrastructure.Auth;

/// <summary>Access token signing settings, bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "hr-service-desk";

    public string Audience { get; set; } = "hr-service-desk";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Supplied through configuration/secrets, never committed.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public bool HasValidKey => Encoding.UTF8.GetByteCount(SigningKey) >= MinimumKeyBytes;
}
