namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Represents normalized payload values for one daemon-start command execution. </summary>
/// <param name="StartStatus"> The daemon-start outcome value (<c>started</c> or <c>alreadyRunning</c>). </param>
/// <param name="DaemonStatus"> The daemon-status value. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon start workflow. </param>
/// <param name="Session"> The daemon session values associated with started or already-running daemon process. </param>
internal sealed record DaemonStartExecutionOutput (
    string StartStatus,
    string DaemonStatus,
    int TimeoutMilliseconds,
    DaemonSessionOutput Session);