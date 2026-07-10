using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;

/// <summary> Cleans daemon artifacts before start execution when stale or invalid sessions are detected. </summary>
internal interface IDaemonSessionCleanupService
{
    /// <summary> Cleans invalid-session artifacts from daemon-session read results. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readResult"> The failed daemon-session read result. </param>
    /// <param name="timeout"> The timeout shared by process termination and artifact cleanup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary> Cleans stale-session artifacts from existing daemon session metadata. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <param name="timeout"> The timeout shared by process termination and artifact cleanup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
