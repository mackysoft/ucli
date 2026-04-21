using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Provides shared serializer settings for <c>scene-tree-lite/&lt;sceneKey&gt;.lookup.json</c> contracts. </summary>
internal static class IndexSceneTreeLiteLookupJsonContractSerializer
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary> Deserializes one scene-tree-lite lookup JSON text to contract. </summary>
    /// <param name="json"> The scene-tree-lite lookup JSON text. </param>
    /// <returns> The deserialized contract; or <see langword="null" /> when JSON root is <c>null</c>. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public static IndexSceneTreeLiteLookupJsonContract? Deserialize (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON text must not be empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<IndexSceneTreeLiteLookupJsonContract>(json, DeserializeOptions);
    }

    /// <summary> Serializes one scene-tree-lite lookup contract to JSON text. </summary>
    /// <param name="contract"> The scene-tree-lite lookup contract. </param>
    /// <returns> The serialized JSON text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contract" /> is <see langword="null" />. </exception>
    public static string Serialize (IndexSceneTreeLiteLookupJsonContract contract)
    {
        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        return JsonSerializer.Serialize(contract, SerializeOptions);
    }
}