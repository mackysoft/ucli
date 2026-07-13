using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Provides JSON serializer options for persisted daemon runtime contracts. </summary>
internal static class DaemonStorageJsonSerializerOptions
{
    /// <summary> Gets options for reading persisted daemon runtime contracts. </summary>
    public static JsonSerializerOptions Deserialize { get; } = Create(writeIndented: false);

    /// <summary> Gets options for writing persisted daemon runtime contracts. </summary>
    public static JsonSerializerOptions Serialize { get; } = Create(writeIndented: true);

    private static JsonSerializerOptions Create (bool writeIndented)
    {
        return new JsonSerializerOptions
        {
            Converters =
            {
                new ContractLiteralJsonConverterFactory(),
            },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = writeIndented,
        };
    }
}
