using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor;

/// <summary> Launches the worktree-local supervisor process through the platform-specific ownership path. </summary>
internal interface ISupervisorProcessLauncher
{
    /// <summary> Launches the supervisor for the specified storage root. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    ValueTask<ExecutionError?> Launch (
        string storageRoot,
        CancellationToken cancellationToken = default);
}