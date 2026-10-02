using System.Text.RegularExpressions;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Tenants;

/// <summary>An organisation using the service desk. Tenants are the isolation boundary for all business data.</summary>
public sealed partial class Tenant : Entity, IAuditable
{
    public const int NameMaxLength = 200;
    public const int SlugMaxLength = 63;

    private Tenant() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>URL-safe unique key, e.g. <c>acme-tn</c>.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>IANA time zone used for business hours, e.g. <c>Africa/Tunis</c>.</summary>
    public string TimeZoneId { get; private set; } = "UTC";

    public string DefaultCulture { get; private set; } = "fr";

    public bool IsActive { get; private set; } = true;

    /// <summary>Closed cases older than this are anonymized (GDPR storage limitation).</summary>
    public int RetentionMonths { get; private set; } = 24;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public static Tenant Create(string name, string slug, string timeZoneId, string defaultCulture)
    {
        name = (name ?? string.Empty).Trim();
        slug = (slug ?? string.Empty).Trim().ToLowerInvariant();

        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("tenant.invalid_name", $"Tenant name must be 1 to {NameMaxLength} characters.");
        if (slug.Length > SlugMaxLength || !SlugPattern().IsMatch(slug))
            throw new DomainException("tenant.invalid_slug", "Tenant slug must be lowercase letters, digits and single hyphens.");
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new DomainException("tenant.invalid_time_zone", "Tenant time zone is required.");

        return new Tenant
        {
            Name = name,
            Slug = slug,
            TimeZoneId = timeZoneId.Trim(),
            DefaultCulture = string.IsNullOrWhiteSpace(defaultCulture) ? "fr" : defaultCulture.Trim(),
        };
    }

    public void SetRetention(int months)
    {
        if (months is < 6 or > 120)
            throw new DomainException("tenant.invalid_retention", "Retention must be between 6 and 120 months.");
        RetentionMonths = months;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
