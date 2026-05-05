namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Cleans safe daemon artifacts for one Unity project context. </summary>
internal interface IDaemonCleanupOperation
{
    /// <summary> Cleans safe daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon cleanup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One daemon cleanup result. </para>
    /// <para> <see cref="DaemonCleanupStatus.Completed" /> means the operation completed one safe cleanup path; it does not guarantee that artifacts were present and deleted. </para>
    /// <para> <see cref="DaemonCleanupStatus.Skipped" /> is a successful non-destructive outcome used when cleanup cannot safely prove that deleting canonical artifacts is allowed. </para>
    /// <para> <see cref="DaemonCleanupStatus.Failed" /> is reserved for timeout, I/O, and unexpected internal failures. </para>
    /// </returns>
    ValueTask<DaemonCleanupResult> Cleanup (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
