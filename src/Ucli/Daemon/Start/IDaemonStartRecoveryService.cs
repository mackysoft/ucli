using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon.Start;

/// <summary> Recovers daemon artifacts before start execution when stale or invalid sessions are detected. </summary>
internal interface IDaemonStartRecoveryService
{
    /// <summary> Recovers stale artifacts from invalid daemon-session read results. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readResult"> The failed daemon-session read result. </param>
    /// <param name="timeout"> The timeout used for process-termination attempts. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The recovery operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> RecoverInvalidSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary> Recovers stale artifacts from existing daemon session metadata. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <param name="timeout"> The timeout used for process-termination attempts. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The recovery operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> RecoverStaleSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}