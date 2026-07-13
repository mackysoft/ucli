using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Common.Serialization;

/// <summary> Provides JSON serializer options for public CLI output contracts. </summary>
internal static class CliOutputJsonSerializerOptions
{
    /// <summary> Gets the serializer options shared by command results and stream entries. </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        Converters =
        {
            new ContractLiteralJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
