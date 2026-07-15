using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;

/// <summary> Sends daemon shutdown requests through IPC transport. </summary>
internal interface IDaemonShutdownClient
{
    /// <summary> Sends one logical shutdown request and follows at most one replacement session after explicit token rejection. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The observed daemon session metadata used for the initial delivery. </param>
    /// <param name="deadline"> The deadline shared by the daemon-stop workflow. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The result of the initial delivery or the single permitted replacement delivery. </returns>
    ValueTask<DaemonShutdownAttemptResult> SendShutdownAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}
