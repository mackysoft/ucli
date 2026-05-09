using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Cleans stale daemon artifacts such as persisted sessions and endpoint residues. </summary>
internal interface IDaemonArtifactCleaner
{
    /// <summary> Cleans stale daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> CleanupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
