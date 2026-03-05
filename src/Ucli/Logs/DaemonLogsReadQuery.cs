namespace MackySoft.Ucli.Logs;

/// <summary> Represents one daemon-log read query sent to IPC client. </summary>
/// <param name="Tail"> The optional tail count that limits returned events to the most recent subset. </param>
/// <param name="After"> The optional opaque cursor used for incremental reads. </param>
/// <param name="Since"> The optional lower time bound in ISO 8601 format. </param>
/// <param name="Until"> The optional upper time bound in ISO 8601 format. </param>
/// <param name="Level"> The optional level filter (<c>error|warning|info|all</c>). </param>
/// <param name="Query"> The optional free-text query value. </param>
/// <param name="QueryTarget"> The optional query target value. </param>
/// <param name="Category"> The optional category filter. </param>
internal sealed record DaemonLogsReadQuery (
    int? Tail,
    string? After,
    string? Since,
    string? Until,
    string? Level,
    string? Query,
    string? QueryTarget,
    string? Category);