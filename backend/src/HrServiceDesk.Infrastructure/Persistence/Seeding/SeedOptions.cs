namespace HrServiceDesk.Infrastructure.Persistence.Seeding;

/// <summary>Demo data settings, bound from the <c>Seed</c> configuration section.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    /// <summary>Password given to every demo account. Supplied via the SEED_PASSWORD environment variable, never committed.</summary>
    public string? DemoPassword { get; set; }
}
