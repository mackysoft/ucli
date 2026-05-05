using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Validates the uCLI-supported JSON Schema subset persisted in read-index catalogs. </summary>
internal static class IndexJsonSchemaSubsetValidator
{
    public static bool IsValidObjectSchema (string? json)
    {
        return IsValidObjectSchema(json, rejectRequestLocalAliasProperties: false);
    }

    public static bool IsValidPublicRawOpArgsSchema (string? json)
    {
        return IsValidObjectSchema(json, rejectRequestLocalAliasProperties: true);
    }

    private static bool IsValidObjectSchema (
        string? json,
        bool rejectRequestLocalAliasProperties)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && IsValidSchemaNode(document.RootElement, document.RootElement, rejectRequestLocalAliasProperties);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidSchemaNode (
        JsonElement schema,
        JsonElement rootSchema,
        bool rejectRequestLocalAliasProperties)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !HasOnlySupportedKeywords(schema))
        {
            return false;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            return CountProperties(schema) == 1
                && IsValidReference(reference, rootSchema);
        }

        return IsValidType(schema)
            && IsValidProperties(schema, rootSchema, rejectRequestLocalAliasProperties)
            && IsValidRequired(schema, rejectRequestLocalAliasProperties)
            && IsValidAdditionalProperties(schema)
            && IsValidItems(schema, rootSchema, rejectRequestLocalAliasProperties)
            && IsValidDefinitions(schema, rootSchema, rejectRequestLocalAliasProperties);
    }

    private static bool HasOnlySupportedKeywords (JsonElement schema)
    {
        foreach (var property in schema.EnumerateObject())
        {
            if (!IsSupportedKeyword(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidType (JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return true;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return IsSupportedType(type.GetString());
        }

        if (type.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in type.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var typeName = item.GetString();
            if (!IsSupportedType(typeName)
                || !values.Add(typeName!))
            {
                return false;
            }
        }

        return values.Count != 0;
    }

    private static bool IsValidProperties (
        JsonElement schema,
        JsonElement rootSchema,
        bool rejectRequestLocalAliasProperties)
    {
        if (!schema.TryGetProperty("properties", out var properties))
        {
            return true;
        }

        if (properties.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in properties.EnumerateObject())
        {
            if ((rejectRequestLocalAliasProperties && UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(property.Name))
                || !IsValidSchemaNode(property.Value, rootSchema, rejectRequestLocalAliasProperties))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidRequired (
        JsonElement schema,
        bool rejectRequestLocalAliasProperties)
    {
        if (!schema.TryGetProperty("required", out var required))
        {
            return true;
        }

        if (required.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in required.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var name = item.GetString();
            if (string.IsNullOrWhiteSpace(name)
                || (rejectRequestLocalAliasProperties && UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(name!))
                || !names.Add(name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidAdditionalProperties (JsonElement schema)
    {
        if (!schema.TryGetProperty("additionalProperties", out var additionalProperties))
        {
            return true;
        }

        return additionalProperties.ValueKind == JsonValueKind.False;
    }

    private static bool IsValidItems (
        JsonElement schema,
        JsonElement rootSchema,
        bool rejectRequestLocalAliasProperties)
    {
        if (!schema.TryGetProperty("items", out var items))
        {
            return true;
        }

        return IsValidSchemaNode(items, rootSchema, rejectRequestLocalAliasProperties);
    }

    private static bool IsValidDefinitions (
        JsonElement schema,
        JsonElement rootSchema,
        bool rejectRequestLocalAliasProperties)
    {
        if (!schema.TryGetProperty("$defs", out var definitions))
        {
            return true;
        }

        if (definitions.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var definition in definitions.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(definition.Name)
                || !IsValidSchemaNode(definition.Value, rootSchema, rejectRequestLocalAliasProperties))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidReference (
        JsonElement reference,
        JsonElement rootSchema)
    {
        if (reference.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = reference.GetString();
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("#/$defs/", StringComparison.Ordinal))
        {
            return false;
        }

        var definitionName = value.Substring("#/$defs/".Length);
        if (definitionName.Length == 0)
        {
            return false;
        }

        return rootSchema.TryGetProperty("$defs", out var definitions)
            && definitions.ValueKind == JsonValueKind.Object
            && definitions.TryGetProperty(definitionName, out var definition)
            && definition.ValueKind == JsonValueKind.Object;
    }

    private static bool IsSupportedKeyword (string keyword)
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

    private static bool IsSupportedType (string? typeName)
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

    private static int CountProperties (JsonElement element)
    {
        var count = 0;
        foreach (var _ in element.EnumerateObject())
        {
            count++;
        }

        return count;
    }
}
