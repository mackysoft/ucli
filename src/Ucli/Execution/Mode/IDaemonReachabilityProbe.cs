using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Probes whether the daemon endpoint for one Unity project is currently reachable. </summary>
internal interface IDaemonReachabilityProbe
{
    /// <summary> Probes daemon reachability for the specified Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The reachability probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    ValueTask<DaemonReachabilityProbeResult> ProbeAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
