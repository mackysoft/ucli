using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

/// <summary> Observes supervisor-internal daemon-start progress. </summary>
internal interface IDaemonStartProgressObserver
{
    /// <summary> Emits the Unity process launch observation entry. </summary>
    ValueTask EmitLaunchingAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken);

    /// <summary> Emits the endpoint-registration wait observation entry. </summary>
    ValueTask EmitWaitingForEndpointAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken);

    /// <summary> Emits the startup-blocker observation entry. </summary>
    ValueTask EmitBlockerDetectedAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken);

    /// <summary> Emits the session-registration observation entry. </summary>
    ValueTask EmitSessionRegisteredAsync (
        DaemonSession session,
        string? launchAttemptId,
        CancellationToken cancellationToken);

    /// <summary> Emits the endpoint-registration observation entry. </summary>
    ValueTask EmitEndpointRegisteredAsync (
        DaemonSession session,
        string? launchAttemptId,
        CancellationToken cancellationToken);

    /// <summary> Emits the endpoint-registered lifecycle observation entry. </summary>
    ValueTask EmitLifecycleObservedAsync (
        IpcUnityEditorObservation lifecycleObservation,
        CancellationToken cancellationToken);
}
