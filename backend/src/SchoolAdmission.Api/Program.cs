using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Cors;
using SchoolAdmission.Api.ErrorHandling;
using SchoolAdmission.Api.Json;
using SchoolAdmission.Api.Middleware;
using SchoolAdmission.Api.OpenApi;
using SchoolAdmission.Application;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// EF logs a "fail" line for every failed SQL command, including expected ones (unique violation, the missing
// history table on a brand-new database). Real failures are still logged by GlobalExceptionHandler.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Critical);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => JsonSetup.Configure(o.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ModelStateProblemFactory.Create);
builder.Services.ConfigureHttpJsonOptions(o => JsonSetup.Configure(o.SerializerOptions));

builder.Services.AddApiProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApiVersioningAndOpenApi();
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<AdmissionDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// Apply migrations (with retry while PostgreSQL starts) BEFORE hosted services such as the sweeper start.
await app.Services.MigrateDatabaseWithRetryAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();            // 404/405 from routing → ProblemDetails with a code
app.UseMiddleware<ServerTimeHeaderMiddleware>();
app.UseCors(CorsSetup.PolicyName);

app.MapApiDocs();                    // /openapi/v1.json, /swagger, /scalar — every environment
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).ExcludeFromDescription();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })
    .ExcludeFromDescription();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>Exposed for WebApplicationFactory in the integration tests.</summary>
public partial class Program;
