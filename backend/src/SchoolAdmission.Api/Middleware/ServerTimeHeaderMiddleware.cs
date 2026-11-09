using System.Globalization;

namespace SchoolAdmission.Api.Middleware;

/// <summary>
/// Adds <c>X-Server-Time</c> (UTC ISO-8601, ms) to every response so the UI can correct its countdown for clock skew.
/// This is informational only and never used for an expiry decision. Hold responses also carry <c>serverTime</c>
/// read from the DATABASE clock, which the UI prefers.
/// </summary>
public sealed class ServerTimeHeaderMiddleware(RequestDelegate next, TimeProvider clock)
{
    public const string HeaderName = "X-Server-Time";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] =
                clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });
        return next(context);
    }
}
