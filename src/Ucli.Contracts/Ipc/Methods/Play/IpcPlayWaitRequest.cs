using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>play.wait</c> IPC request payload. </summary>
/// <param name="Until"> The wait target literal. </param>
public sealed record IpcPlayWaitRequest (string Until)
{
    /// <summary> Gets the transition wait timeout in milliseconds. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
