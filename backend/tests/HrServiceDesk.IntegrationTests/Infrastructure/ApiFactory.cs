using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>Hosts the real API against the Testcontainers database, with migrations and demo seed on startup.</summary>
public sealed class ApiFactory(string connectionString, IReadOnlyDictionary<string, string>? overrides = null)
    : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-tests-signing-key-0123456789abcdef";

    /// <summary>Attachment storage for this test run, deleted with the factory.</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "hrdesk-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Swagger:Enabled", "true");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DemoPassword", DemoUsers.Password);
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
        builder.UseSetting("Storage:RootPath", StorageRoot);

        foreach (var (key, value) in overrides ?? new Dictionary<string, string>())
            builder.UseSetting(key, value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }

    /// <summary>A client that does not store cookies: tests pass the refresh cookie explicitly.</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
}
