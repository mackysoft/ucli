using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;

/// <summary> Sends daemon shutdown requests through IPC transport. </summary>
internal interface IDaemonShutdownClient
{
    /// <summary> Sends one logical shutdown request across explicitly rejected session generations within the shared deadline. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The observed daemon session metadata used for the initial delivery. </param>
    /// <param name="deadline"> The deadline shared by the daemon-stop workflow. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The result of the accepted delivery or the terminal failure observed within the shared deadline. </returns>
    ValueTask<DaemonShutdownAttemptResult> SendShutdownAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}
