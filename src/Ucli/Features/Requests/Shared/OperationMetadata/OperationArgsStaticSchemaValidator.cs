using System.Text.Json;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Validates one operation args object against the registered static schema subset. </summary>
internal static class OperationArgsStaticSchemaValidator
{
    /// <summary> Validates one operation <c>args</c> payload against the registered static schema subset. </summary>
    /// <param name="schemaJson"> The registered operation-args schema JSON. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="args"> The operation-args payload to validate. </param>
    /// <param name="schemaInvalid"> <see langword="true" /> when the registered schema itself is malformed or uses an unsupported construct. </param>
    /// <param name="error"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when <paramref name="args" /> satisfies the supported schema subset; otherwise <see langword="false" />. </returns>
    public static bool TryValidate (
        string schemaJson,
        JsonElement args,
        out bool schemaInvalid,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            schemaInvalid = true;
            error = "Operation args schema must not be empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return TryValidateElement(document.RootElement, args, "args", out schemaInvalid, out error);
        }
        catch (JsonException exception)
        {
            schemaInvalid = true;
            error = $"Operation args schema is invalid JSON. {exception.Message}";
            return false;
        }
    }

    private static bool TryValidateElement (
        JsonElement schema,
        JsonElement value,
        string path,
        out bool schemaInvalid,
        out string? error)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            schemaInvalid = true;
            error = $"Schema node for '{path}' must be an object.";
            return false;
        }

        if (!TryValidateSchemaKeywords(schema, path, out schemaInvalid, out error))
        {
            return false;
        }

        if (schema.TryGetProperty("type", out var typeElement)
            && !TryValidateType(typeElement, value, path, out schemaInvalid, out error))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Object
            && !TryValidateObjectKeywords(schema, value, path, out schemaInvalid, out error))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Array
            && !TryValidateArrayKeywords(schema, value, path, out schemaInvalid, out error))
        {
            return false;
        }

        schemaInvalid = false;
        error = null;
        return true;
    }

    private static bool TryValidateSchemaKeywords (
        JsonElement schema,
        string path,
        out bool schemaInvalid,
        out string? error)
    {
        foreach (var property in schema.EnumerateObject())
        {
            if (IsSupportedSchemaKeyword(property.Name))
            {
                continue;
            }

            schemaInvalid = true;
            error = $"Schema property '{property.Name}' for '{path}' is not supported by the uCLI structural schema subset.";
            return false;
        }

        schemaInvalid = false;
        error = null;
        return true;
    }

    private static bool TryValidateType (
        JsonElement typeElement,
        JsonElement value,
        string path,
        out bool schemaInvalid,
        out string? error)
    {
        if (typeElement.ValueKind == JsonValueKind.String)
        {
            if (!IsSupportedTypeName(typeElement.GetString()!))
            {
                schemaInvalid = true;
                error = $"Schema property 'type' for '{path}' references an unsupported type.";
                return false;
            }

            if (TryMatchesType(typeElement.GetString()!, value, out var typeError))
            {
                schemaInvalid = false;
                error = null;
                return true;
            }

            schemaInvalid = false;
            error = $"Property '{path}' {typeError}.";
            return false;
        }

        if (typeElement.ValueKind != JsonValueKind.Array)
        {
            schemaInvalid = true;
            error = $"Schema property 'type' for '{path}' must be a string or an array.";
            return false;
        }

        var allowedTypes = new List<string>();
        foreach (var candidateType in typeElement.EnumerateArray())
        {
            if (candidateType.ValueKind != JsonValueKind.String)
            {
                schemaInvalid = true;
                error = $"Schema property 'type' for '{path}' contains a non-string entry.";
                return false;
            }

            var candidateTypeName = candidateType.GetString()!;
            if (!IsSupportedTypeName(candidateTypeName))
            {
                schemaInvalid = true;
                error = $"Schema property 'type' for '{path}' references an unsupported type.";
                return false;
            }

            allowedTypes.Add(candidateTypeName);
            if (TryMatchesType(candidateTypeName, value, out _))
            {
                schemaInvalid = false;
                error = null;
                return true;
            }
        }

        schemaInvalid = false;
        error = $"Property '{path}' must be one of: {string.Join(", ", allowedTypes)}.";
        return false;
    }

    private static bool TryValidateArrayKeywords (
        JsonElement schema,
        JsonElement value,
        string path,
        out bool schemaInvalid,
        out string? error)
    {
        if (!schema.TryGetProperty("items", out var itemsElement))
        {
            schemaInvalid = false;
            error = null;
            return true;
        }

        if (itemsElement.ValueKind != JsonValueKind.Object)
        {
            schemaInvalid = true;
            error = $"Schema property 'items' for '{path}' must be an object.";
            return false;
        }

        var itemIndex = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (!TryValidateElement(itemsElement, item, $"{path}[{itemIndex}]", out schemaInvalid, out error))
            {
                return false;
            }

            itemIndex++;
        }

        schemaInvalid = false;
        error = null;
        return true;
    }

    private static bool TryValidateObjectKeywords (
        JsonElement schema,
        JsonElement value,
        string path,
        out bool schemaInvalid,
        out string? error)
    {
        if (schema.TryGetProperty("required", out var requiredElement))
        {
            if (requiredElement.ValueKind != JsonValueKind.Array)
            {
                schemaInvalid = true;
                error = $"Schema property 'required' for '{path}' must be an array.";
                return false;
            }

            foreach (var requiredProperty in requiredElement.EnumerateArray())
            {
                if (requiredProperty.ValueKind != JsonValueKind.String)
                {
                    schemaInvalid = true;
                    error = $"Schema property 'required' for '{path}' contains a non-string entry.";
                    return false;
                }

                var propertyName = requiredProperty.GetString()!;
                if (!value.TryGetProperty(propertyName, out _))
                {
                    schemaInvalid = false;
                    error = $"Property '{path}.{propertyName}' is required.";
                    return false;
                }
            }
        }

        Dictionary<string, JsonElement>? propertiesByName = null;
        if (schema.TryGetProperty("properties", out var propertiesElement))
        {
            if (propertiesElement.ValueKind != JsonValueKind.Object)
            {
                schemaInvalid = true;
                error = $"Schema property 'properties' for '{path}' must be an object.";
                return false;
            }

            propertiesByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in propertiesElement.EnumerateObject())
            {
                propertiesByName[property.Name] = property.Value;
            }
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalPropertiesElement))
        {
            if (additionalPropertiesElement.ValueKind != JsonValueKind.True
                && additionalPropertiesElement.ValueKind != JsonValueKind.False)
            {
                schemaInvalid = true;
                error = $"Schema property 'additionalProperties' for '{path}' must be a boolean.";
                return false;
            }

            if (!additionalPropertiesElement.GetBoolean()
                && propertiesByName != null)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (!propertiesByName.ContainsKey(property.Name))
                    {
                        schemaInvalid = false;
                        error = $"Property '{path}.{property.Name}' is not allowed.";
                        return false;
                    }
                }
            }
            else if (!additionalPropertiesElement.GetBoolean())
            {
                foreach (var property in value.EnumerateObject())
                {
                    schemaInvalid = false;
                    error = $"Property '{path}.{property.Name}' is not allowed.";
                    return false;
                }
            }
        }

        if (propertiesByName != null)
        {
            foreach (var property in propertiesByName)
            {
                if (!value.TryGetProperty(property.Key, out var propertyValue))
                {
                    continue;
                }

                if (!TryValidateElement(property.Value, propertyValue, $"{path}.{property.Key}", out schemaInvalid, out error))
                {
                    return false;
                }
            }
        }

        schemaInvalid = false;
        error = null;
        return true;
    }

    private static bool TryMatchesType (
        string typeName,
        JsonElement value,
        out string error)
    {
        switch (typeName)
        {
            case "object":
                if (value.ValueKind == JsonValueKind.Object)
                {
                    error = string.Empty;
                    return true;
                }

                error = "must be an object";
                return false;

            case "array":
                if (value.ValueKind == JsonValueKind.Array)
                {
                    error = string.Empty;
                    return true;
                }

                error = "must be an array";
                return false;

            case "string":
                if (value.ValueKind == JsonValueKind.String)
                {
                    error = string.Empty;
                    return true;
                }

                error = "must be a string";
                return false;

            case "integer":
                if (value.ValueKind == JsonValueKind.Number
                    && IsIntegerNumber(value))
                {
                    error = string.Empty;
                    return true;
                }

                error = "must be an integer";
                return false;

            case "number":
                if (value.ValueKind == JsonValueKind.Number)
                {
                    error = string.Empty;
                    return true;
                }

                error = "must be a number";
                return false;

            case "boolean":
                if (value.ValueKind == JsonValueKind.True
                    || value.ValueKind == JsonValueKind.False)
                {
                    error = string.Empty;
                    return true;
                }

                error = "must be a boolean";
                return false;

            case "null":
                if (value.ValueKind == JsonValueKind.Null)
                {
                    error = string.Empty;
                    return true;
                }

                error = "must be null";
                return false;

            default:
                error = $"references unsupported schema type '{typeName}'";
                return false;
        }
    }

    private static bool IsIntegerNumber (JsonElement value)
    {
        var rawText = value.GetRawText();
        return rawText.IndexOf('.') < 0
            && rawText.IndexOf('e') < 0
            && rawText.IndexOf('E') < 0;
    }

    private static bool IsSupportedTypeName (string typeName)
    {
        switch (typeName)
        {
            case "object":
            case "array":
            case "string":
            case "integer":
            case "number":
            case "boolean":
            case "null":
                return true;

            default:
                return false;
        }
    }

    private static bool IsSupportedSchemaKeyword (string keyword)
    {
        switch (keyword)
        {
            case "type":
            case "properties":
            case "required":
            case "additionalProperties":
            case "items":
            case "$ref":
            case "$defs":
                return true;

            default:
                return false;
        }
    }
}
