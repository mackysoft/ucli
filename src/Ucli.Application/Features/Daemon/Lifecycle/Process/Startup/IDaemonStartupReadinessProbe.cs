namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Probes daemon startup until the daemon endpoint responds with a lifecycle snapshot, timeout expires, or startup fails. </summary>
internal interface IDaemonStartupReadinessProbe
{
    /// <summary> Waits until daemon startup registers an endpoint and returns one lifecycle snapshot, or fails when timeout expires or startup fails. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The startup endpoint-registration timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="daemonProcessId"> The launched Unity daemon process identifier when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The endpoint-registration probe result. </returns>
    ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReadyAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        int? daemonProcessId = null,
        CancellationToken cancellationToken = default);
}
