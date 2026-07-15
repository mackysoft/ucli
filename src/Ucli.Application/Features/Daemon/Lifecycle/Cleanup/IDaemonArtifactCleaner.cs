using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Cleans stale daemon artifacts such as persisted sessions and endpoint residues. </summary>
internal interface IDaemonArtifactCleaner
{
    /// <summary> Cleans stale daemon artifacts only while the persisted session is still absent. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The deadline shared by ownership revalidation and artifact deletion admission. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup result. A session published before ownership is acquired is a successful no-op. </returns>
    ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMissingAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken);

    /// <summary> Cleans stale daemon artifacts while the observed generation is still current or has already removed its session. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="expectedSession"> The session generation that authorized cleanup. </param>
    /// <param name="deadline"> The deadline shared by ownership revalidation and artifact deletion admission. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup result. A replacement generation is a successful no-op. </returns>
    ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession expectedSession,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken);

    /// <summary> Cleans stale daemon artifacts while no session exists or the current session belongs to a process already stopped by uCLI. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="stoppedProcess"> The process identity that has already been stopped. </param>
    /// <param name="deadline"> The deadline shared by ownership revalidation and artifact deletion admission. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup result. A session owned by another process is a successful no-op. </returns>
    ValueTask<DaemonArtifactCleanupResult> CleanupIfStoppedProcessMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget stoppedProcess,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken);

    /// <summary> Cleans stale daemon artifacts only while the serialized session file still matches the observed artifact. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="expectedArtifactIdentity"> The exact serialized session artifact that authorized cleanup. </param>
    /// <param name="deadline"> The deadline shared by ownership revalidation and artifact deletion admission. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup result. A replaced session is a successful no-op; a missing session allows residual cleanup. </returns>
    ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionArtifactMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionArtifactIdentity expectedArtifactIdentity,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken);
}
