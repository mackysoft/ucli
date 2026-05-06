using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Deserializes <c>asset-search.lookup.json</c> contracts. </summary>
internal static class IndexAssetSearchLookupJsonContractSerializer
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary> Deserializes one asset-search lookup JSON text to contract. </summary>
    /// <param name="json"> The asset-search lookup JSON text. </param>
    /// <returns> The deserialized contract; or <see langword="null" /> when JSON root is <c>null</c>. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public static IndexAssetSearchLookupJsonContract? Deserialize (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON text must not be empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<IndexAssetSearchLookupJsonContract>(json, DeserializeOptions);
    }
}
