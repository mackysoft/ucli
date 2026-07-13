using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Provides shared serializer settings for daemon <c>daemon-lifecycle.json</c> contracts. </summary>
internal static class DaemonLifecycleJsonContractSerializer
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        Converters =
        {
            new ContractLiteralJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        Converters =
        {
            new ContractLiteralJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary> Deserializes daemon lifecycle JSON text to contract. </summary>
    public static DaemonLifecycleJsonContract? Deserialize (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON text must not be empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<DaemonLifecycleJsonContract>(json, DeserializeOptions);
    }

    /// <summary> Serializes daemon lifecycle contract to JSON text. </summary>
    public static string Serialize (DaemonLifecycleJsonContract contract)
    {
        if (contract is null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        return JsonSerializer.Serialize(contract, SerializeOptions);
    }
}
