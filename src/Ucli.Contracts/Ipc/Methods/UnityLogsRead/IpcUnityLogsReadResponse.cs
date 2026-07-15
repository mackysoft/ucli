using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>unity.logs.read</c> IPC response payload. </summary>
public sealed record IpcUnityLogsReadResponse
{
    /// <summary> Initializes one Unity-log read response. </summary>
    [JsonConstructor]
    public IpcUnityLogsReadResponse (
        IReadOnlyList<IpcUnityLogEvent> Events,
        string NextCursor)
    {
        this.Events = ContractArgumentGuard.RequireItems(Events, nameof(Events));
        this.NextCursor = ContractArgumentGuard.RequireValue(NextCursor, nameof(NextCursor));
    }

    /// <summary> Gets the filtered Unity log events. </summary>
    public IReadOnlyList<IpcUnityLogEvent> Events { get; }

    /// <summary> Gets the opaque cursor used for the next incremental read. </summary>
    public string NextCursor { get; }
}
