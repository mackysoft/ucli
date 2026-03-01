using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides shared JSON serializer options for IPC envelopes. </summary>
public static class IpcJsonSerializerOptions
{
    /// <summary> Gets the default serializer options used by IPC client and server components. </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}