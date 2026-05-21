using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>play.enter</c> IPC request payload. </summary>
public sealed record IpcPlayEnterRequest
{
    /// <summary> Gets the transition wait timeout in milliseconds. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
