using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>compile</c> IPC request payload. </summary>
/// <param name="RunId"> The CLI-generated compile run identifier. </param>
public sealed record IpcCompileRequest (string RunId)
{
    /// <summary> Gets the request timeout budget propagated by the caller, in milliseconds. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
