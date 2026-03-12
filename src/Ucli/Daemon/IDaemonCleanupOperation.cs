using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Cleans safe daemon artifacts for one Unity project context. </summary>
internal interface IDaemonCleanupOperation
{
    /// <summary> Cleans safe daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon cleanup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon cleanup result. </returns>
    ValueTask<DaemonCleanupResult> Cleanup (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}