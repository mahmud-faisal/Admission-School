using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SchoolAdmission.Application.Common.Behaviors;

/// <summary>
/// Outermost behavior: logs request name, elapsed time and outcome as structured fields.
/// Student IDs are masked to their last 4 digits.
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var student = request is IStudentScopedRequest scoped ? Mask(scoped.StudentId) : "-";
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(cancellationToken);
            var outcome = response is Result { Error: { } error } ? error.Code : "Success";
            LogHandled(logger, name, student, outcome, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            LogFailed(logger, name, student, ex.GetType().Name, sw.ElapsedMilliseconds);
            throw;
        }
    }

    internal static string Mask(string? studentId) =>
        string.IsNullOrEmpty(studentId) ? "(none)"
        : studentId.Length <= 4 ? new string('*', studentId.Length)
        : new string('*', studentId.Length - 4) + studentId[^4..];

    [LoggerMessage(Level = LogLevel.Information, Message = "{Request} student={Student} outcome={Outcome} in {ElapsedMs} ms")]
    private static partial void LogHandled(ILogger logger, string request, string student, string outcome, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Request} student={Student} threw {Exception} after {ElapsedMs} ms")]
    private static partial void LogFailed(ILogger logger, string request, string student, string exception, long elapsedMs);
}
