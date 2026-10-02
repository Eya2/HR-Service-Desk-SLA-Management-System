namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>Demo data settings, bound from the <c>Seed</c> configuration section.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    /// <summary>Password given to every demo account. Supplied via the SEED_PASSWORD environment variable, never committed.</summary>
    public string? DemoPassword { get; set; }

    /// <summary>Also generate six weeks of demo activity (cases, ratings, more employees). Off in tests.</summary>
    public bool History { get; set; }

    /// <summary>Full API key the demo payroll connector uses (hrd_&lt;8 chars&gt;_…). Seeded for Acme when set.</summary>
    public string? PayrollApiKey { get; set; }

    /// <summary>Where the demo webhook is sent (the mock payroll container).</summary>
    public string? PayrollWebhookUrl { get; set; }

    /// <summary>Signing secret shared with the mock payroll container.</summary>
    public string? PayrollWebhookSecret { get; set; }
}
