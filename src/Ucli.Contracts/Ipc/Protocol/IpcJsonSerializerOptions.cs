using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides shared JSON serializer options for IPC envelopes. </summary>
public static class IpcJsonSerializerOptions
{
    /// <summary> Gets the default serializer options used by IPC client and server components. </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        Converters =
        {
            new VocabularyJsonConverterFactory(),
            new UcliStringValueJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };

    /// <summary> Gets serializer options for operation args whose property names must match the published schema exactly. </summary>
    public static JsonSerializerOptions StrictPropertyNames { get; } = new(Default)
    {
        PropertyNameCaseInsensitive = false,
    };
}
