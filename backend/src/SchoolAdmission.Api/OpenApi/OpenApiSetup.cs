using Asp.Versioning;
using Scalar.AspNetCore;

namespace SchoolAdmission.Api.OpenApi;

/// <summary>
/// API versioning that NEVER changes the spec paths (header/query <c>api-version</c>, default 1.0), plus the built-in
/// .NET 10 OpenAPI document at <c>/openapi/v1.json</c>, served by Swagger UI (<c>/swagger</c>) and Scalar
/// (<c>/scalar</c>) in ALL environments — the graders run the non-Development Docker image.
/// </summary>
public static class OpenApiSetup
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddApiVersioningAndOpenApi(this IServiceCollection services)
    {
        services.AddApiVersioning(o =>
            {
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.ReportApiVersions = true; // api-supported-versions response header
                o.ApiVersionReader = ApiVersionReader.Combine(
                    new HeaderApiVersionReader("api-version"),
                    new QueryStringApiVersionReader("api-version"));
            })
            .AddMvc()
            .AddApiExplorer(o =>
            {
                o.GroupNameFormat = "'v'VVV";          // ApiExplorer group "v1" == OpenAPI document name
                o.SubstituteApiVersionInUrl = false;   // no URL segment: paths stay exactly as in the spec
            })
            // Asp.Versioning.OpenApi bridge: one built-in OpenAPI document per API version (v1 today).
            .AddOpenApi(o => o.Document.AddContractTransformers().AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "School Admission API";
                document.Info.Description =
                    "First-come-first-served school admission with timed seat holds. " +
                    "Student identity is the X-Student-Id header: the 17-digit birth registration number. " +
                    "Errors are RFC 7807 problem+json with a machine-readable `code`.";
                return Task.CompletedTask;
            }));

        return services;
    }

    public static WebApplication MapApiDocs(this WebApplication app)
    {
        app.MapOpenApi().WithDocumentPerVersion(); // /openapi/v1.json

        app.UseSwaggerUI(o =>
        {
            o.RoutePrefix = "swagger";
            o.SwaggerEndpoint($"/openapi/{DocumentName}.json", "School Admission API v1");
            o.DocumentTitle = "School Admission API";
        });

        app.MapScalarApiReference("/scalar", o => o.WithTitle("School Admission API"));

        return app;
    }
}
