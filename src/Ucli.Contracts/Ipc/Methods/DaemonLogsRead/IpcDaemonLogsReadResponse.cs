using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>daemon.logs.read</c> IPC response payload. </summary>
public sealed record IpcDaemonLogsReadResponse
{
    /// <summary> Initializes one daemon-log read response. </summary>
    /// <exception cref="ArgumentNullException"> Thrown when an argument is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an event is <see langword="null" />, belongs to another stream, does not strictly advance, or does not precede <paramref name="NextCursor" />.
    /// </exception>
    [JsonConstructor]
    public IpcDaemonLogsReadResponse (
        IReadOnlyList<IpcDaemonLogEvent> Events,
        IpcLogCursor NextCursor)
    {
        var events = ContractArgumentGuard.RequireItems(Events, nameof(Events));
        var nextCursor = ContractArgumentGuard.RequireNotNull(NextCursor, nameof(NextCursor));
        IpcLogResponseContract.Validate(
            events,
            nextCursor,
            static item => item.Cursor,
            nameof(Events));
        this.Events = events;
        this.NextCursor = nextCursor;
    }

    /// <summary> Gets the filtered daemon log events. </summary>
    public IReadOnlyList<IpcDaemonLogEvent> Events { get; }

    /// <summary> Gets the opaque cursor used for the next incremental read. </summary>
    public IpcLogCursor NextCursor { get; }
}
