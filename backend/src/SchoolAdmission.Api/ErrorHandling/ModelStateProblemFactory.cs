using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.Common;

namespace SchoolAdmission.Api.ErrorHandling;

/// <summary>
/// Model-binding failures (malformed JSON, <c>"totalSeats": "abc"</c>, empty body) → 400 VALIDATION_FAILED, with
/// field keys normalised to camelCase JSON names ("$.totalSeats" → "totalSeats").
/// </summary>
public static class ModelStateProblemFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .GroupBy(kv => NormalizeKey(kv.Key))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(kv => kv.Value!.Errors)
                      .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "The value is invalid." : FriendlyMessage(g.Key, e.ErrorMessage))
                      .Distinct()
                      .ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = errors.Values.SelectMany(v => v).FirstOrDefault(),
            Extensions = { [ProblemDetailsSetup.CodeKey] = ErrorCodes.ValidationFailed },
        };

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string NormalizeKey(string key)
    {
        var k = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key.TrimStart('$');
        if (k.Length == 0 || k.Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }

        return char.ToLowerInvariant(k[0]) + k[1..];
    }

    private static string FriendlyMessage(string key, string raw)
    {
        if (key == "totalSeats" && raw.Contains("could not be converted", StringComparison.Ordinal))
        {
            return "Total seats must be a whole number.";
        }

        if (key == "name" && raw.Contains("could not be converted", StringComparison.Ordinal))
        {
            return "Class name must be text.";
        }

        // System.Text.Json parser errors ("... Path: $.x | LineNumber: 0 | BytePositionInLine: 8.") are not user-friendly.
        if (raw.Contains("LineNumber:", StringComparison.Ordinal) || raw.Contains("is invalid JSON", StringComparison.Ordinal))
        {
            return "The request body is not valid JSON.";
        }

        return raw;
    }
}
