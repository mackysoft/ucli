using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start;

/// <summary> Applies cleanup compensation when daemon launch workflow fails. </summary>
internal interface IDaemonLaunchCompensationService
{
    /// <summary> Stops the launched process snapshot and cleans daemon artifacts after launch failure. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon-session issuance timestamp used for identity validation. </param>
    /// <param name="timeout"> The remaining timeout budget for launch-failure compensation. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The compensation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunch (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset? expectedIssuedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}