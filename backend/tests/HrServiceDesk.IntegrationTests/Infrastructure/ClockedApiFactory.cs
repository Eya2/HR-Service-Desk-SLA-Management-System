using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>
/// An API host whose application clock is a <see cref="TestClock"/>, for time-based behaviour (SLA, jobs).
/// Tokens are stamped with that clock, so their lifetime is not checked against the real one here.
/// </summary>
public sealed class ClockedApiFactory(string connectionString, DateTimeOffset start, IReadOnlyDictionary<string, string>? overrides = null)
    : ApiFactory(connectionString, overrides)
{
    public TestClock Clock { get; } = new(start);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.TokenValidationParameters.ValidateLifetime = false);
        });
    }
}
