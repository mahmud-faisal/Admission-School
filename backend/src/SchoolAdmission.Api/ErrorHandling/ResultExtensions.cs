using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Api.ErrorHandling;

public static class ResultExtensions
{
    /// <summary>Turns a business <see cref="Error"/> into an RFC 7807 response with the error's <c>code</c>.</summary>
    public static ObjectResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        var factory = controller.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(controller.HttpContext, status, Title(error.Code), detail: error.Message);
        problem.Extensions[ProblemDetailsSetup.CodeKey] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    private static string Title(string code) => code switch
    {
        ErrorCodes.NotFound => "Not found",
        ErrorCodes.Forbidden => "Forbidden",
        ErrorCodes.SeatsFull => "No seats available",
        ErrorCodes.DuplicateApplication => "Duplicate application",
        ErrorCodes.HoldExpired => "Application expired",
        ErrorCodes.HoldCancelled => "Application cancelled",
        ErrorCodes.HoldAlreadyConfirmed => "Admission already confirmed",
        _ => "Conflict",
    };
}
