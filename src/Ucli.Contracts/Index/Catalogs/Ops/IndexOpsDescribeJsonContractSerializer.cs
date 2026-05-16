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
        if (document.RootElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var root = document.RootElement;
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: root.GetProperty("schemaVersion").GetInt32(),
            GeneratedAtUtc: root.GetProperty("generatedAtUtc").GetDateTimeOffset(),
            SourceInputsHash: ReadNullableString(root, "sourceInputsHash"),
            Operation: DeserializeOperation(root.GetProperty("operation")));
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
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
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
}
