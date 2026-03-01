using System.Text.Json;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides shared JSON serializer options for Unity IPC framing and payload models. </summary>
    internal static class UnityIpcSerializerOptions
    {
        /// <summary> Gets serializer options used by Unity-side IPC transport and request handlers. </summary>
        public static JsonSerializerOptions Default { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };
    }
}
