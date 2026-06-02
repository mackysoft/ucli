using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Gateway;

/// <summary> Observes host-visible daemon-start lifecycle-gateway progress. </summary>
internal interface IDaemonProjectLifecycleProgressObserver
{
    /// <summary> Emits the supervisor-bootstrap start entry. </summary>
    ValueTask EmitSupervisorBootstrapStartedAsync (CancellationToken cancellationToken);

    /// <summary> Emits the supervisor-bootstrap completion entry. </summary>
    ValueTask EmitSupervisorBootstrapCompletedAsync (
        ExecutionError? error,
        CancellationToken cancellationToken);

    /// <summary> Emits the supervisor ensureRunning request start entry. </summary>
    ValueTask EmitEnsureRunningStartedAsync (CancellationToken cancellationToken);

    /// <summary> Emits the supervisor ensureRunning request completion entry. </summary>
    ValueTask EmitEnsureRunningCompletedAsync (
        DaemonStartResult result,
        CancellationToken cancellationToken);
}
