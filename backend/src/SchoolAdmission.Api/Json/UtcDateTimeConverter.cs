using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Json;

/// <summary>
/// Writes every DateTime as UTC ISO-8601 ending in "Z". Trailing zero fractions are dropped, so a whole-second
/// value looks like <c>2026-11-01T04:10:00Z</c>. Nullable DateTime uses this converter too.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToUtc(value).ToString(Format, CultureInfo.InvariantCulture));

    /// <summary>Unspecified is treated as UTC (Npgsql returns UTC); Local is converted.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
