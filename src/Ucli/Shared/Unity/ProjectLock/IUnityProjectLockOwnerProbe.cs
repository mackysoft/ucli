using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Determines whether an existing Unity lock file has a live owner. </summary>
internal interface IUnityProjectLockOwnerProbe
{
    /// <summary> Probes lock ownership for one project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="lockFilePath"> The Unity lock-file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The lock owner probe result. </returns>
    ValueTask<UnityProjectLockOwnerProbeResult> ProbeOwnerAsync (
        ResolvedUnityProjectContext unityProject,
        string lockFilePath,
        CancellationToken cancellationToken = default);
}
