using System.Text.Json;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Validates one operation args object against the registered static schema subset. </summary>
internal static class OperationArgsStaticSchemaValidator
{
    private const int MaxReferenceDepth = 64;

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
            var context = new SchemaValidationContext(document.RootElement);
            return TryValidateElement(context, document.RootElement, args, "args", out schemaInvalid, out error);
        }
        catch (JsonException exception)
        {
            schemaInvalid = true;
            error = $"Operation args schema is invalid JSON. {exception.Message}";
            return false;
        }
    }

    private static bool TryValidateElement (
        SchemaValidationContext context,
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

        if (schema.TryGetProperty("$ref", out var referenceElement))
        {
            if (schema.EnumerateObject().Count() != 1)
            {
                schemaInvalid = true;
                error = $"Schema '$ref' for '{path}' must not be combined with other schema keywords.";
                return false;
            }

            if (!TryResolveReference(
                context,
                referenceElement,
                path,
                out var definitionName,
                out var referencedSchema,
                out schemaInvalid,
                out error))
            {
                return false;
            }

            if (!context.TryEnterReference(definitionName, out error))
            {
                schemaInvalid = true;
                return false;
            }

            try
            {
                return TryValidateElement(context, referencedSchema, value, path, out schemaInvalid, out error);
            }
            finally
            {
                context.ExitReference(definitionName);
            }
        }

        if (schema.TryGetProperty("type", out var typeElement)
            && !TryValidateType(typeElement, value, path, out schemaInvalid, out error))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Object
            && !TryValidateObjectKeywords(context, schema, value, path, out schemaInvalid, out error))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Array
            && !TryValidateArrayKeywords(context, schema, value, path, out schemaInvalid, out error))
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
        SchemaValidationContext context,
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
            if (!TryValidateElement(context, itemsElement, item, $"{path}[{itemIndex}]", out schemaInvalid, out error))
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
        SchemaValidationContext context,
        JsonElement schema,
        JsonElement value,
        string path,
        out bool schemaInvalid,
        out string? error)
    {
        if (!TryValidateUniquePropertyNames(value, path, out error))
        {
            schemaInvalid = false;
            return false;
        }

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

                if (!TryValidateElement(context, property.Value, propertyValue, $"{path}.{property.Key}", out schemaInvalid, out error))
                {
                    return false;
                }
            }
        }

        schemaInvalid = false;
        error = null;
        return true;
    }

    private static bool TryValidateUniquePropertyNames (
        JsonElement value,
        string path,
        out string? error)
    {
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!propertyNames.Add(property.Name))
            {
                error = $"Property '{path}.{property.Name}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryResolveReference (
        SchemaValidationContext context,
        JsonElement referenceElement,
        string path,
        out string definitionName,
        out JsonElement referencedSchema,
        out bool schemaInvalid,
        out string? error)
    {
        definitionName = string.Empty;
        referencedSchema = default;
        if (referenceElement.ValueKind != JsonValueKind.String)
        {
            schemaInvalid = true;
            error = $"Schema '$ref' for '{path}' must be a string.";
            return false;
        }

        var reference = referenceElement.GetString();
        if (string.IsNullOrWhiteSpace(reference)
            || !reference.StartsWith("#/$defs/", StringComparison.Ordinal))
        {
            schemaInvalid = true;
            error = $"Schema '$ref' for '{path}' must target '#/$defs/<name>'.";
            return false;
        }

        definitionName = reference.Substring("#/$defs/".Length);
        if (definitionName.Length == 0)
        {
            schemaInvalid = true;
            error = $"Schema '$ref' for '{path}' must include a definition name.";
            return false;
        }

        if (!context.TryGetDefinition(definitionName, out referencedSchema))
        {
            schemaInvalid = true;
            error = $"Schema '$ref' for '{path}' references unknown definition '{definitionName}'.";
            return false;
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

    private sealed class SchemaValidationContext
    {
        private readonly JsonElement rootSchema;

        private readonly HashSet<string> activeReferences = new(StringComparer.Ordinal);

        public SchemaValidationContext (JsonElement rootSchema)
        {
            this.rootSchema = rootSchema;
        }

        public bool TryEnterReference (
            string name,
            out string? error)
        {
            if (activeReferences.Count >= MaxReferenceDepth)
            {
                error = $"Schema '$ref' nesting exceeds the maximum supported depth of {MaxReferenceDepth}.";
                return false;
            }

            if (!activeReferences.Add(name))
            {
                error = $"Schema '$ref' contains a circular reference to definition '{name}'.";
                return false;
            }

            error = null;
            return true;
        }

        public void ExitReference (string name)
        {
            activeReferences.Remove(name);
        }

        public bool TryGetDefinition (
            string name,
            out JsonElement schema)
        {
            schema = default;
            if (!rootSchema.TryGetProperty("$defs", out var definitions)
                || definitions.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!definitions.TryGetProperty(name, out var definition)
                || definition.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            schema = definition;
            return true;
        }
    }
}
