using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>unity.logs.read</c> IPC request payload. </summary>
public sealed record IpcUnityLogsReadRequest
{
    /// <summary> Initializes one Unity-log read request. </summary>
    [JsonConstructor]
    public IpcUnityLogsReadRequest (
        int? Tail,
        string? After,
        string? Since,
        string? Until,
        IpcLogLevel? Level,
        string? Query,
        IpcLogQueryTarget? QueryTarget,
        IpcUnityLogSource? Source,
        IpcUnityLogStackTraceMode? StackTrace,
        int? StackTraceMaxFrames,
        int? StackTraceMaxChars)
    {
        if (Level.HasValue && !ContractLiteralCodec.IsDefined(Level.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Log level must be defined.");
        }

        if (QueryTarget.HasValue && !ContractLiteralCodec.IsDefined(QueryTarget.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(QueryTarget), QueryTarget, "Log query target must be defined.");
        }

        if (Source.HasValue && !ContractLiteralCodec.IsDefined(Source.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Unity log source must be defined.");
        }

        if (StackTrace.HasValue && !ContractLiteralCodec.IsDefined(StackTrace.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StackTrace), StackTrace, "Unity log stack-trace mode must be defined.");
        }

        this.Tail = Tail;
        this.After = After;
        this.Since = Since;
        this.Until = Until;
        this.Level = Level;
        this.Query = Query;
        this.QueryTarget = QueryTarget;
        this.Source = Source;
        this.StackTrace = StackTrace;
        this.StackTraceMaxFrames = StackTraceMaxFrames;
        this.StackTraceMaxChars = StackTraceMaxChars;
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

    /// <summary> Gets the optional source filter; omission allows all sources. </summary>
    public IpcUnityLogSource? Source { get; }

    /// <summary> Gets the optional stack-trace mode; omission includes error stack traces. </summary>
    public IpcUnityLogStackTraceMode? StackTrace { get; }

    /// <summary> Gets the optional maximum number of stack-trace frames. </summary>
    public int? StackTraceMaxFrames { get; }

    /// <summary> Gets the optional maximum number of stack-trace characters. </summary>
    public int? StackTraceMaxChars { get; }
}
