using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Probes daemon reachability using cleanup-specific safety semantics. </summary>
internal interface IDaemonCleanupReachabilityProbe
{
    /// <summary> Probes whether cleanup may treat the canonical endpoint as stale. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="sessionToken"> The probe session token to send. Must be non-empty. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup-specific reachability probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="sessionToken" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    ValueTask<DaemonCleanupReachabilityProbeResult> Probe (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        string sessionToken,
        CancellationToken cancellationToken = default);
}