using HrServiceDesk.Api.Auth;
using HrServiceDesk.Api.Common;
using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Api.Middleware;
using HrServiceDesk.Api.Notifications;
using HrServiceDesk.Application;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Infrastructure;
using HrServiceDesk.Infrastructure.Jobs;
using HrServiceDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) =>
{
    logger.ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (context.Configuration.GetValue<bool>("Logging:Json"))
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    else
        logger.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture, outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj} {CorrelationId}{NewLine}{Exception}");
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApiAuth();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddScoped<HrServiceDesk.Application.Abstractions.INotificationChannel, HubNotificationChannel>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HR Service Desk API",
        Version = "v1",
        Description = "HR case management with approval workflows, SLAs and escalation.",
    });
    options.SwaggerDoc("integration", new OpenApiInfo
    {
        Title = "HR Service Desk Integration API",
        Version = "v1",
        Description = "For external systems (payroll, HRIS). Authenticate with the X-Api-Key header. "
            + "Webhooks are signed: X-HrDesk-Signature: t=<unix>,v1=<hex HMAC-SHA256(secret, \"t.body\")>.",
    });
    options.DocInclusionPredicate((document, api) => (api.GroupName ?? "v1") == document);
    var apiKey = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "Integration key created by an HR Admin (Administration > Integrations).",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" },
    };
    options.AddSecurityDefinition("ApiKey", apiKey);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [apiKey] = [] });
    var bearer = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token from POST /api/auth/login.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml))
        options.IncludeXmlComments(xml);
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await app.Services.InitializeDatabaseAsync();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "HR Service Desk API";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Desk API");
        options.SwaggerEndpoint("/swagger/integration/swagger.json", "Integration API");
    });
}

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Path);
app.UseBackgroundJobs();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

await app.RunAsync();

/// <summary>Entry point, exposed for <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program;
