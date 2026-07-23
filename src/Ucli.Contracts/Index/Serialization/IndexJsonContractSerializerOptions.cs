using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Provides shared JSON options for read-index contract deserializers. </summary>
internal static class IndexJsonContractSerializerOptions
{
    public static JsonSerializerOptions Deserialize { get; } = new()
    {
        Converters =
        {
            new VocabularyJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
