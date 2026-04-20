using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Sends daemon shutdown requests through IPC transport. </summary>
internal interface IDaemonShutdownClient
{
    /// <summary> Sends one shutdown request using persisted daemon session token. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The persisted daemon session metadata. </param>
    /// <param name="timeout"> The IPC timeout used for shutdown request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The shutdown attempt result. </returns>
    ValueTask<DaemonShutdownAttemptResult> SendShutdown (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}