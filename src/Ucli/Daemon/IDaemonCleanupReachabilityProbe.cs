using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Probes daemon reachability using cleanup-specific safety semantics. </summary>
internal interface IDaemonCleanupReachabilityProbe
{
    /// <summary> Probes whether cleanup has endpoint-level evidence that the canonical daemon endpoint is absent. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="sessionToken"> The probe session token to send. Must be non-empty. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One cleanup-specific reachability probe result. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.NotRunning" /> means the probe observed direct endpoint-level absence evidence that is strong enough for destructive cleanup. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Running" /> means the daemon accepted the ping request successfully. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Uncertain" /> means cleanup could not safely prove endpoint absence and must not delete artifacts based on probe evidence alone. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Failed" /> means probing itself failed unexpectedly. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="sessionToken" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    ValueTask<DaemonCleanupReachabilityProbeResult> Probe (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        string sessionToken,
        CancellationToken cancellationToken = default);
}