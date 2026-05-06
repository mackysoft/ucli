using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Deserializes <c>schemas.catalog.json</c> contracts. </summary>
internal static class IndexSchemasCatalogJsonContractSerializer
{
    /// <summary> Deserializes one schemas-catalog JSON text to contract. </summary>
    /// <param name="json"> The schemas-catalog JSON text. </param>
    /// <returns> The deserialized contract; or <see langword="null" /> when JSON root is <c>null</c>. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public static IndexSchemasCatalogJsonContract? Deserialize (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON text must not be empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<IndexSchemasCatalogJsonContract>(json, IndexJsonContractSerializerOptions.Deserialize);
    }
}
