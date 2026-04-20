namespace MackySoft.Ucli.Features.Daemon.UseCases.Stop;

/// <summary> Represents normalized payload values for one daemon-stop command execution. </summary>
/// <param name="StopStatus"> The daemon-stop outcome value (<c>stopped</c> or <c>notRunning</c>). </param>
/// <param name="DaemonStatus"> The daemon-status value. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon stop workflow. </param>
/// <param name="Session"> The daemon session values when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonStopExecutionOutput (
    string StopStatus,
    string DaemonStatus,
    int TimeoutMilliseconds,
    DaemonSessionOutput? Session);