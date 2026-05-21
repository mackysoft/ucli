using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>play.exit</c> IPC request payload. </summary>
public sealed record IpcPlayExitRequest
{
    /// <summary> Gets the transition wait timeout in milliseconds. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
