using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in-process against a test database. Settings use the same keys as the env vars
/// (HOLD_DURATION_SECONDS, ...). Environment "Testing" so appsettings.Development.json is not loaded.
/// </summary>
public sealed class AdmissionApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null,
    Action<IServiceCollection>? configureServices = null,
    string environment = "Testing") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", "600");
        builder.UseSetting("HOLD_SWEEPER_ENABLED", "false");
        builder.UseSetting("CORS_ORIGINS", "http://localhost:3000");

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
