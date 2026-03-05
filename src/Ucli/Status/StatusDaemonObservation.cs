namespace MackySoft.Ucli.Status;

/// <summary> Represents daemon observation values that can be projected into status command payload. </summary>
/// <param name="DaemonStatus"> The daemon status value serialized into command payload. </param>
/// <param name="ServerVersion"> The daemon-side server version when reachable. </param>
/// <param name="CompileState"> The daemon compile-state value when reachable. </param>
/// <param name="Runtime"> The daemon runtime value when reachable. </param>
internal sealed record StatusDaemonObservation (
    string DaemonStatus,
    string? ServerVersion,
    string? CompileState,
    string? Runtime);