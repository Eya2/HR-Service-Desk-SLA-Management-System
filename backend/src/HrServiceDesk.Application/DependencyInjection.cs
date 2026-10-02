using FluentValidation;
using HrServiceDesk.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace HrServiceDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddScoped<Auth.SessionIssuer>();
        services.AddScoped<Tickets.Assignment.AutoAssigner>();
        services.AddScoped<Sla.SlaService>();

        return services;
    }
}
