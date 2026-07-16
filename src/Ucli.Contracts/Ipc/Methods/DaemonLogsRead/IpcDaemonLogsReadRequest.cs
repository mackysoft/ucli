using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>daemon.logs.read</c> IPC request payload. </summary>
public sealed record IpcDaemonLogsReadRequest
{
    /// <summary> Initializes one daemon-log read request. </summary>
    [JsonConstructor]
    public IpcDaemonLogsReadRequest (
        int? Tail,
        string? After,
        string? Since,
        string? Until,
        IpcLogLevel? Level,
        string? Query,
        IpcLogQueryTarget? QueryTarget,
        string? Category)
    {
        if (Level.HasValue && !ContractLiteralCodec.IsDefined(Level.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Log level must be defined.");
        }

        if (QueryTarget.HasValue && !ContractLiteralCodec.IsDefined(QueryTarget.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(QueryTarget), QueryTarget, "Log query target must be defined.");
        }

        this.Tail = Tail;
        this.After = After;
        this.Since = Since;
        this.Until = Until;
        this.Level = Level;
        this.Query = Query;
        this.QueryTarget = QueryTarget;
        this.Category = Category;
    }

    /// <summary> Gets the optional tail count that limits returned events to the most recent subset. </summary>
    public int? Tail { get; }

    /// <summary> Gets the optional opaque cursor used for incremental reads. </summary>
    public string? After { get; }

    /// <summary> Gets the optional lower time bound in ISO 8601 format. </summary>
    public string? Since { get; }

    /// <summary> Gets the optional upper time bound in ISO 8601 format. </summary>
    public string? Until { get; }

    /// <summary> Gets the optional level filter; omission allows all levels. </summary>
    public IpcLogLevel? Level { get; }

    /// <summary> Gets the optional free-text query value. </summary>
    public string? Query { get; }

    /// <summary> Gets the optional query target; omission searches message text. </summary>
    public IpcLogQueryTarget? QueryTarget { get; }

    /// <summary> Gets the optional category filter. </summary>
    public string? Category { get; }
}
