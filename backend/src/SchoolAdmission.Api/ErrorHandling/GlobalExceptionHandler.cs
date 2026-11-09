using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Api.ErrorHandling;

/// <summary>
/// ValidationException → 400 (INVALID_STUDENT_ID if any failure has that code, else VALIDATION_FAILED).
/// BadHttpRequestException → its status code. Anything else → 500 INTERNAL_ERROR without a stack trace.
/// </summary>
public sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case ValidationException validation:
                var isStudentId = validation.Errors.Any(e => e.ErrorCode == ErrorCodes.InvalidStudentId);
                var errors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                problem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = isStudentId ? "Invalid student ID" : "Validation failed",
                    Detail = errors.Values.SelectMany(v => v).FirstOrDefault(),
                    Extensions = { [ProblemDetailsSetup.CodeKey] = isStudentId ? ErrorCodes.InvalidStudentId : ErrorCodes.ValidationFailed },
                };
                break;

            case BadHttpRequestException bad:
                problem = new ProblemDetails
                {
                    Status = bad.StatusCode,
                    Title = "Bad request",
                    Detail = bad.Message,
                };
                break;

            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                return true; // client went away; nothing to write

            default:
                LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal server error",
                    Detail = "Something went wrong on the server. Please try again.",
                    Extensions = { [ProblemDetailsSetup.CodeKey] = ErrorCodes.InternalError },
                };
                break;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}
