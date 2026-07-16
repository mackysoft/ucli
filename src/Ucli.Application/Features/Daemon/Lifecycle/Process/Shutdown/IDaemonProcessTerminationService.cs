using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;

/// <summary> Stops daemon process by process identifier, including force-kill fallback. </summary>
internal interface IDaemonProcessTerminationService
{
    /// <summary> Ensures daemon process is stopped before timeout expires. </summary>
    /// <param name="target"> The daemon process termination target when available. </param>
    /// <param name="deadline"> The deadline shared by the owning stop or cleanup workflow. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process termination result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
        DaemonProcessTerminationTarget? target,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}
