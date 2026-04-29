namespace MackySoft.Tests;

using System.Text.Json;

internal static class JsonSchemaValidator
{
    private static readonly IReadOnlyDictionary<string, JsonSchemaProperty> EmptyProperties =
        new Dictionary<string, JsonSchemaProperty>();

    public static IReadOnlyList<string> Validate (JsonElement element, JsonSchemaNode schema, string path)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var errors = new List<string>();
        ValidateAgainstSchema(
            element: element,
            schema: schema,
            path: path,
            errors: errors);
        return errors;
    }

    private static void ValidateAgainstSchema (
        JsonElement element,
        JsonSchemaNode schema,
        string path,
        List<string> errors)
    {
        if (schema.Types is null || schema.Types.Count == 0)
        {
            errors.Add($"path '{path}' has an invalid schema definition: no allowed types.");
            return;
        }

        if (!MatchesAnyType(element, schema.Types))
        {
            errors.Add(
                $"path '{path}' expected {FormatExpectedTypes(schema.Types)} but was {DescribeActualType(element)}.");
            return;
        }

        if (HasType(schema.Types, JsonSchemaType.Object) && element.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(element, schema, path, errors);
        }

        if (HasType(schema.Types, JsonSchemaType.Array) && element.ValueKind == JsonValueKind.Array)
        {
            ValidateArray(element, schema, path, errors);
        }
    }

    private static void ValidateObject (
        JsonElement element,
        JsonSchemaNode schema,
        string path,
        List<string> errors)
    {
        var schemaProperties = schema.Properties ?? EmptyProperties;

        foreach (var (propertyName, propertySchema) in schemaProperties)
        {
            if (!element.TryGetProperty(propertyName, out var propertyValue))
            {
                if (propertySchema.Required)
                {
                    errors.Add($"path '{BuildPropertyPath(path, propertyName)}' is missing.");
                }

                continue;
            }

            ValidateAgainstSchema(
                element: propertyValue,
                schema: propertySchema.Schema,
                path: BuildPropertyPath(path, propertyName),
                errors: errors);
        }

        if (!schema.AllowAdditionalProperties)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!schemaProperties.ContainsKey(property.Name))
                {
                    errors.Add(
                        $"path '{BuildPropertyPath(path, property.Name)}' is not allowed by schema.");
                }
            }
        }
    }

    private static void ValidateArray (
        JsonElement element,
        JsonSchemaNode schema,
        string path,
        List<string> errors)
    {
        if (schema.ItemSchema is null)
        {
            errors.Add($"path '{path}' has an invalid schema definition: item schema is missing.");
            return;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            ValidateAgainstSchema(
                element: item,
                schema: schema.ItemSchema,
                path: $"{path}[{index}]",
                errors: errors);
            index++;
        }
    }

    private static bool MatchesAnyType (JsonElement element, IReadOnlyList<JsonSchemaType> allowedTypes)
    {
        foreach (var allowedType in allowedTypes)
        {
            if (MatchesType(element, allowedType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesType (JsonElement element, JsonSchemaType allowedType)
    {
        return allowedType switch
        {
            JsonSchemaType.Object => element.ValueKind == JsonValueKind.Object,
            JsonSchemaType.Array => element.ValueKind == JsonValueKind.Array,
            JsonSchemaType.String => element.ValueKind == JsonValueKind.String,
            JsonSchemaType.Int32 => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _),
            JsonSchemaType.Boolean => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            JsonSchemaType.Null => element.ValueKind == JsonValueKind.Null,
            _ => false,
        };
    }

    private static bool HasType (IReadOnlyList<JsonSchemaType> allowedTypes, JsonSchemaType type)
    {
        foreach (var allowedType in allowedTypes)
        {
            if (allowedType == type)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatExpectedTypes (IReadOnlyList<JsonSchemaType> allowedTypes)
    {
        return "one of [" + string.Join(", ", allowedTypes.Select(static x => x.ToString())) + "]";
    }

    private static string DescribeActualType (JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && !element.TryGetInt32(out _))
        {
            return "Number(non-Int32)";
        }

        return element.ValueKind.ToString();
    }

    private static string BuildPropertyPath (string parentPath, string propertyName)
    {
        return $"{parentPath}.{propertyName}";
    }
}
