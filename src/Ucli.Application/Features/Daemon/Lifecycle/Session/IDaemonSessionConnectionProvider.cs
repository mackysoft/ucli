namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Resolves daemon IPC connection values for one Unity project context. </summary>
internal interface IDaemonSessionConnectionProvider
{
    /// <summary> Resolves daemon IPC connection values from persisted session metadata. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon IPC connection resolution result. </returns>
    ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
