using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>daemon.logs.read</c> IPC response payload. </summary>
public sealed record IpcDaemonLogsReadResponse
{
    /// <summary> Initializes one daemon-log read response. </summary>
    [JsonConstructor]
    public IpcDaemonLogsReadResponse (
        IReadOnlyList<IpcDaemonLogEvent> Events,
        string NextCursor)
    {
        this.Events = ContractArgumentGuard.RequireItems(Events, nameof(Events));
        this.NextCursor = ContractArgumentGuard.RequireValue(NextCursor, nameof(NextCursor));
    }

    /// <summary> Gets the filtered daemon log events. </summary>
    public IReadOnlyList<IpcDaemonLogEvent> Events { get; }

    /// <summary> Gets the opaque cursor used for the next incremental read. </summary>
    public string NextCursor { get; }
}
