using SchoolAdmission.Api.Middleware;

namespace SchoolAdmission.Api.Cors;

public static class CorsSetup
{
    public const string PolicyName = "frontend";
    public const string DefaultOrigins = "http://localhost:3000,http://localhost:5173";

    /// <summary>
    /// Origins come from <c>CORS_ORIGINS</c> (comma-separated; <c>*</c> = any origin). Any header is allowed because
    /// the UI sends X-Student-Id and api-version. No credentials are used.
    /// </summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var raw = configuration["CORS_ORIGINS"];
        var origins = (string.IsNullOrWhiteSpace(raw) ? DefaultOrigins : raw)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'))
            .ToArray();

        services.AddCors(o => o.AddPolicy(PolicyName, p =>
        {
            if (origins.Contains("*"))
            {
                p.AllowAnyOrigin();
            }
            else
            {
                p.WithOrigins(origins);
            }

            p.AllowAnyHeader()
             .AllowAnyMethod()
             .WithExposedHeaders(ServerTimeHeaderMiddleware.HeaderName, "Location", "api-supported-versions");
        }));

        return services;
    }
}
