using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Probes daemon startup readiness until timeout expires. </summary>
internal interface IDaemonStartupReadinessProbe
{
    /// <summary> Waits until daemon endpoint becomes reachable, or timeout expires. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The startup readiness timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="daemonProcessId"> The launched Unity daemon process identifier when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The readiness probe result. </returns>
    ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReady (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        int? daemonProcessId = null,
        CancellationToken cancellationToken = default);
}
