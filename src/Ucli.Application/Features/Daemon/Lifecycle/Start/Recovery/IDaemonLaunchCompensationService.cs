using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;

/// <summary> Applies cleanup compensation when daemon launch workflow fails. </summary>
internal interface IDaemonLaunchCompensationService
{
    /// <summary> Stops the launched process snapshot and cleans daemon artifacts after launch failure. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="target"> The launched process termination target when available. </param>
    /// <param name="timeout"> The remaining timeout budget for launch-failure compensation. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The compensation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget? target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
