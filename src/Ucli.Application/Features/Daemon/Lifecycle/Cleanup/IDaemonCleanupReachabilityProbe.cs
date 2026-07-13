namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Probes daemon reachability using cleanup-specific safety semantics. </summary>
internal interface IDaemonCleanupReachabilityProbe
{
    /// <summary> Probes the canonical daemon endpoint without presenting a session token. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One cleanup-specific reachability probe result. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.NotRunning" /> means the probe observed direct endpoint-level absence evidence that is strong enough for destructive cleanup. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Running" /> means the endpoint returned a valid correlated IPC response, including a session-token error response. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Uncertain" /> means cleanup could not safely prove endpoint absence and must not delete artifacts based on probe evidence alone. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Failed" /> means probing itself failed unexpectedly. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    ValueTask<DaemonCleanupReachabilityProbeResult> ProbeWithoutSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);

    /// <summary> Probes the canonical daemon endpoint using a known session token. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="sessionToken"> The session token to present. Must be non-empty. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One cleanup-specific reachability probe result. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.NotRunning" /> means the probe observed direct endpoint-level absence evidence that is strong enough for destructive cleanup. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Running" /> means the daemon accepted the ping request carrying the supplied session token. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Uncertain" /> means cleanup could not safely prove endpoint absence, including when the session token is rejected. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.Failed" /> means probing itself failed unexpectedly. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="sessionToken" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    ValueTask<DaemonCleanupReachabilityProbeResult> ProbeWithSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        string sessionToken,
        CancellationToken cancellationToken = default);
}
