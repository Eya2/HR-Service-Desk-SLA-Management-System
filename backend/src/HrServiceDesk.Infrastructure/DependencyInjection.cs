using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Infrastructure.Auth;
using HrServiceDesk.Infrastructure.Persistence;
using HrServiceDesk.Infrastructure.Persistence.Interceptors;
using HrServiceDesk.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HrServiceDesk.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        AddPersistence(services);
        AddAuth(services);

        return services;
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddScoped<TenantStampingInterceptor>();
        services.AddScoped<AuditableInterceptor>();

        // Connection string is resolved lazily so test hosts can override configuration.
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(GetConnectionString(sp))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                sp.GetRequiredService<TenantStampingInterceptor>(),
                sp.GetRequiredService<AuditableInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
        services.AddScoped<DemoDataSeeder>();

        services.AddHealthChecks()
            .AddNpgSql(GetConnectionString, name: "postgres", tags: ["ready"]);
    }

    private static void AddAuth(IServiceCollection services)
    {
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(o => o.HasValidKey, $"Jwt:SigningKey must be at least {JwtOptions.MinimumKeyBytes} bytes.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
    }

    private static string GetConnectionString(IServiceProvider sp) =>
        sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
}
