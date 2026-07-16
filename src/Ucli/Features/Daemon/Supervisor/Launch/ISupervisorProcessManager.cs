using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Manages the worktree-local supervisor through the platform-specific process-ownership path. </summary>
internal interface ISupervisorProcessManager
{
    /// <summary> Launches the supervisor for the specified storage root. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    ValueTask<ExecutionError?> LaunchAsync (
        string storageRoot,
        CancellationToken cancellationToken);

    /// <summary> Releases the operating-system registration after the worktree-local supervisor retires. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="releaseMode"> Whether release must wait for the registered process to terminate. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by supervisor retirement. </param>
    /// <returns> One structured error when release fails; otherwise <see langword="null" />. </returns>
    ValueTask<ExecutionError?> ReleaseAsync (
        string storageRoot,
        SupervisorProcessReleaseMode releaseMode,
        CancellationToken cancellationToken);
}
