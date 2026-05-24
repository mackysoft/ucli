using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Deserializes <c>ops.describe/&lt;opKey&gt;.json</c> contracts. </summary>
internal static class IndexOpsDescribeJsonContractSerializer
{
    /// <summary> Deserializes one ops-describe JSON text to contract. </summary>
    /// <param name="json"> The ops-describe JSON text. </param>
    /// <returns> The deserialized contract; or <see langword="null" /> when JSON root is <c>null</c>. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public static IndexOpsDescribeJsonContract? Deserialize (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON text must not be empty.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        if (!TryReadRoot(document.RootElement, out var root))
        {
            return null;
        }

        return DeserializeRoot(root);
    }

    private static IndexOpsDescribeJsonContract DeserializeRoot (JsonElement root)
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: ReadRequiredInt32(root, "schemaVersion"),
            GeneratedAtUtc: ReadRequiredDateTimeOffset(root, "generatedAtUtc"),
            SourceInputsHash: ReadNullableString(root, "sourceInputsHash"),
            Operation: DeserializeOperation(ReadRequiredProperty(root, "operation")));
    }

    private static bool TryReadRoot (
        JsonElement rootElement,
        out JsonElement root)
    {
        root = default;
        if (rootElement.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (rootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Ops describe JSON root must be an object or null.");
        }

        root = rootElement;
        return true;
    }

    private static IndexOpEntryJsonContract? DeserializeOperation (JsonElement operationElement)
    {
        if (operationElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var operation = JsonSerializer.Deserialize<IndexOpEntryJsonContract>(
            operationElement.GetRawText(),
            IndexJsonContractSerializerOptions.Deserialize);
        if (operation == null)
        {
            return null;
        }

        return new IndexOpEntryJsonContract(
            Name: operation.Name,
            Kind: operation.Kind,
            Policy: operation.Policy,
            ArgsSchemaJson: ReadSchemaJsonOrNull(operationElement, "argsSchema"),
            ResultSchemaJson: ReadSchemaJsonOrNull(operationElement, "resultSchema"))
        {
            Description = operation.Description,
            Inputs = operation.Inputs,
            ResultContract = operation.ResultContract,
            Assurance = operation.Assurance,
            CodeContract = operation.CodeContract,
        };
    }

    private static string? ReadNullableString (
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Ops describe JSON property '{propertyName}' must be a string or null.");
        }

        return property.GetString();
    }

    private static string? ReadSchemaJsonOrNull (
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return JsonSerializer.Serialize(property);
    }

    private static JsonElement ReadRequiredProperty (
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Ops describe JSON is missing required property '{propertyName}'.");
        }

        return property;
    }

    private static int ReadRequiredInt32 (
        JsonElement element,
        string propertyName)
    {
        var property = ReadRequiredProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            throw new JsonException($"Ops describe JSON property '{propertyName}' must be an int32 number.");
        }

        return value;
    }

    private static DateTimeOffset ReadRequiredDateTimeOffset (
        JsonElement element,
        string propertyName)
    {
        var property = ReadRequiredProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.String || !property.TryGetDateTimeOffset(out var value))
        {
            throw new JsonException($"Ops describe JSON property '{propertyName}' must be an ISO 8601 date-time string.");
        }

        return value;
    }
}
