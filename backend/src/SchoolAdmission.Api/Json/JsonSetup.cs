using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Json;

public static class JsonSetup
{
    /// <summary>
    /// camelCase properties, UPPERCASE enum values (HoldStatus.Held → "HELD"), UTC "Z" timestamps, nulls written.
    /// Applied to both MVC and minimal-API/OpenAPI JSON options so the docs match the wire format.
    /// </summary>
    public static void Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        // Web defaults accept "7" for a number; the contract uses real JSON numbers, so be strict.
        o.NumberHandling = JsonNumberHandling.Strict;
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        o.Converters.Add(new UtcDateTimeConverter());
    }
}
