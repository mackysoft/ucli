using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class ConfigurableDaemonStartProgressObserver : IDaemonStartProgressObserver
{
    public Func<DaemonStartProgressEvent, CancellationToken, ValueTask>? Handler { get; set; }

    public ValueTask EmitLaunchingAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.Launching, cancellationToken);
    }

    public ValueTask EmitWaitingForEndpointAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.WaitingForEndpoint, cancellationToken);
    }

    public ValueTask EmitBlockerDetectedAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.BlockerDetected, cancellationToken);
    }

    public ValueTask EmitSessionRegisteredAsync (
        DaemonSession session,
        string? launchAttemptId,
        CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.SessionRegistered, cancellationToken);
    }

    public ValueTask EmitEndpointRegisteredAsync (
        DaemonSession session,
        string? launchAttemptId,
        CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.EndpointRegistered, cancellationToken);
    }

    public ValueTask EmitLifecycleObservedAsync (
        IpcUnityEditorObservation lifecycleObservation,
        CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.LifecycleObserved, cancellationToken);
    }

    private ValueTask EmitAsync (
        DaemonStartProgressEvent progressEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Handler?.Invoke(progressEvent, cancellationToken) ?? ValueTask.CompletedTask;
    }
}
