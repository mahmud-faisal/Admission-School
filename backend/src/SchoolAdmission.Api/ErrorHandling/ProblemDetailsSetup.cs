using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Api.ErrorHandling;

/// <summary>
/// Makes sure EVERY ProblemDetails (ours, MVC's, routing 404/405, api-versioning's) carries a machine-readable
/// <c>code</c> and a <c>traceId</c>.
/// </summary>
public static class ProblemDetailsSetup
{
    public const string CodeKey = "code";

    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            var problem = ctx.ProblemDetails;
            problem.Instance ??= ctx.HttpContext.Request.Path;
            problem.Extensions.TryAdd("traceId",
                ctx.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity?.Id ?? ctx.HttpContext.TraceIdentifier);

            // Asp.Versioning writes its own PascalCase "code" (e.g. "UnsupportedApiVersion"); replace it with ours.
            // Its problem types end in #unsupported / #invalid / #ambiguous / #unspecified.
            if (problem.Type?.StartsWith("https://docs.api-versioning.org/problems", StringComparison.Ordinal) == true)
            {
                problem.Extensions[CodeKey] = ErrorCodes.UnsupportedApiVersion;
            }
            else if (!problem.Extensions.ContainsKey(CodeKey))
            {
                problem.Extensions[CodeKey] = DefaultCode(problem);
            }
        });

    private static string DefaultCode(ProblemDetails problem) =>
        problem.Status switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
            StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ErrorCodes.NotFound,
            StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
            StatusCodes.Status409Conflict => ErrorCodes.Conflict,
            StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
            _ => ErrorCodes.InternalError,
        };
}
