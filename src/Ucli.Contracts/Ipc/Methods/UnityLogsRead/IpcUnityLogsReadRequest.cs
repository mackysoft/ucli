namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>unity.logs.read</c> IPC request payload. </summary>
/// <param name="Tail"> The optional tail count that limits returned events to the most recent subset. </param>
/// <param name="After"> The optional opaque cursor used for incremental reads. </param>
/// <param name="Since"> The optional lower time bound in ISO 8601 format. </param>
/// <param name="Until"> The optional upper time bound in ISO 8601 format. </param>
/// <param name="Level"> The optional level filter (<c>error|warning|info|all</c>). </param>
/// <param name="Query"> The optional free-text query value. </param>
/// <param name="QueryTarget"> The optional query target (<c>message|stack|both</c>). </param>
/// <param name="Source"> The optional source filter (<c>compile|runtime|all</c>). </param>
/// <param name="StackTrace"> The optional stack-trace mode (<c>none|error|all</c>). </param>
/// <param name="StackTraceMaxFrames"> The optional maximum number of stack-trace frames. </param>
/// <param name="StackTraceMaxChars"> The optional maximum number of stack-trace characters. </param>
public sealed record IpcUnityLogsReadRequest (
    int? Tail,
    string? After,
    string? Since,
    string? Until,
    string? Level,
    string? Query,
    string? QueryTarget,
    string? Source,
    string? StackTrace,
    int? StackTraceMaxFrames,
    int? StackTraceMaxChars);