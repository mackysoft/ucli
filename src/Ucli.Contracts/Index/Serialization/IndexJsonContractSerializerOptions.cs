using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Provides shared JSON options for read-index contract deserializers. </summary>
internal static class IndexJsonContractSerializerOptions
{
    public static JsonSerializerOptions Deserialize { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
