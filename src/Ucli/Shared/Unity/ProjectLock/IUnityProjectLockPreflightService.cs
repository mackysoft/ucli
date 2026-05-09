using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Prepares Unity project lock-file state before uCLI starts Unity processes. </summary>
internal interface IUnityProjectLockPreflightService
{
    /// <summary> Performs lock-file preflight before starting a new Unity process. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The lock preflight result. </returns>
    ValueTask<UnityProjectLockPreflightResult> PrepareForUnityProcessStartAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Cleans up a stale lock file left after a uCLI-owned Unity process exits. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The lock preflight result. </returns>
    ValueTask<UnityProjectLockPreflightResult> CleanupStaleLockAfterUnityProcessExitAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
