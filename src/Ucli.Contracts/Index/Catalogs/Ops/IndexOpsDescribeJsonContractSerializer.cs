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

        return JsonSerializer.Deserialize<IndexOpsDescribeJsonContract>(json, IndexJsonContractSerializerOptions.Deserialize);
    }
}
