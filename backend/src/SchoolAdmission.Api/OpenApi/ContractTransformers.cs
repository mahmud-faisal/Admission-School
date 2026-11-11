using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SchoolAdmission.Api.Contracts;

namespace SchoolAdmission.Api.OpenApi;

/// <summary>
/// Makes the OpenAPI document match the wire format exactly:
/// X-Student-Id is required and documented as 17 digits; every ProblemDetails schema shows the <c>code</c> and
/// <c>traceId</c> extensions; nullable C# properties (e.g. confirmedAt) are marked nullable.
/// </summary>
internal static class ContractTransformers
{
    public static OpenApiOptions AddContractTransformers(this OpenApiOptions options)
    {
        options.AddOperationTransformer(StudentIdHeaderAsync);
        options.AddSchemaTransformer(SchemaAsync);
        return options;
    }

    private static Task StudentIdHeaderAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (parameter.In != ParameterLocation.Header ||
                !string.Equals(parameter.Name, Headers.StudentId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            parameter.Required = true;
            parameter.Description = "The student's 17-digit birth registration number (ASCII digits 0-9 only).";
            if (parameter.Schema is OpenApiSchema schema)
            {
                schema.Pattern = "^[0-9]{17}$";
                schema.MinLength = 17;
                schema.MaxLength = 17;
            }
        }

        return Task.CompletedTask;
    }

    private static Task SchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var type = context.JsonTypeInfo.Type;

        if (typeof(ProblemDetails).IsAssignableFrom(type))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["code"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Machine-readable error code, e.g. SEATS_FULL, HOLD_EXPIRED, DUPLICATE_APPLICATION, ALREADY_ADMITTED, " +
                              "INVALID_STUDENT_ID, VALIDATION_FAILED, NOT_FOUND, FORBIDDEN.",
            };
            schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String };
            schema.Required ??= new HashSet<string>();
            schema.Required.Add("code");
        }

        if (schema.Properties is null)
        {
            return Task.CompletedTask;
        }

        foreach (var property in context.JsonTypeInfo.Properties)
        {
            if (!schema.Properties.TryGetValue(property.Name, out var propertySchema) ||
                propertySchema is not OpenApiSchema concrete)
            {
                continue;
            }

            var underlying = Nullable.GetUnderlyingType(property.PropertyType);
            var clrType = underlying ?? property.PropertyType;

            // Our custom UtcDateTimeConverter makes the generator emit only `format: date-time` with no type.
            if (clrType == typeof(DateTime) && concrete.Type is null)
            {
                concrete.Type = JsonSchemaType.String;
                concrete.Format = "date-time";
            }

            if (underlying is not null && concrete.Type is { } current)
            {
                concrete.Type = current | JsonSchemaType.Null;
            }
        }

        return Task.CompletedTask;
    }
}
