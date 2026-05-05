namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Resolves daemon session token values for one Unity project context. </summary>
internal interface IDaemonSessionTokenProvider
{
    /// <summary> Resolves one daemon session token value for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session token resolution result. </returns>
    ValueTask<DaemonSessionTokenResolutionResult> Resolve (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
