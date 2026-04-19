using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Probes whether the daemon endpoint for one Unity project is currently reachable. </summary>
internal interface IDaemonReachabilityProbe
{
    /// <summary> Probes daemon reachability for the specified Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The probe timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The reachability probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    ValueTask<DaemonReachabilityProbeResult> Probe (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}