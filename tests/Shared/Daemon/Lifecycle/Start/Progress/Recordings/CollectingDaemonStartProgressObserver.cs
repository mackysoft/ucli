using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using DaemonStartStartupProgressObservation = MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress.DaemonStartStartupProgressObservation;
using IDaemonStartProgressObserver = MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress.IDaemonStartProgressObserver;

namespace MackySoft.Ucli.TestSupport;

internal sealed class CollectingDaemonStartProgressObserver : IDaemonStartProgressObserver
{
    private readonly List<CollectedDaemonStartProgressEntry> entries = [];

    public void AssertEvents (params DaemonStartProgressEvent[] expectedEvents)
    {
        Assert.Equal(expectedEvents, entries.Select(static entry => entry.Event).ToArray());
    }

    public TPayload PayloadAt<TPayload> (int index)
        where TPayload : notnull
    {
        return Assert.IsType<TPayload>(entries[index].Payload);
    }

    public TPayload PayloadAt<TPayload> (System.Index index)
        where TPayload : notnull
    {
        return Assert.IsType<TPayload>(entries[index.GetOffset(entries.Count)].Payload);
    }

    public ValueTask EmitLaunchingAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(DaemonStartProgressEvent.Launching, observation);
        return ValueTask.CompletedTask;
    }

    public ValueTask EmitWaitingForEndpointAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(DaemonStartProgressEvent.WaitingForEndpoint, observation);
        return ValueTask.CompletedTask;
    }

    public ValueTask EmitBlockerDetectedAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(DaemonStartProgressEvent.BlockerDetected, observation);
        return ValueTask.CompletedTask;
    }

    public ValueTask EmitSessionRegisteredAsync (
        DaemonSession session,
        Guid? launchAttemptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(DaemonStartProgressEvent.SessionRegistered, session);
        return ValueTask.CompletedTask;
    }

    public ValueTask EmitEndpointRegisteredAsync (
        DaemonSession session,
        Guid? launchAttemptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(DaemonStartProgressEvent.EndpointRegistered, session);
        return ValueTask.CompletedTask;
    }

    public ValueTask EmitLifecycleObservedAsync (
        IpcUnityEditorObservation lifecycleObservation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(DaemonStartProgressEvent.LifecycleObserved, lifecycleObservation);
        return ValueTask.CompletedTask;
    }

    private void Add (
        DaemonStartProgressEvent progressEvent,
        object payload)
    {
        entries.Add(new CollectedDaemonStartProgressEntry(progressEvent, payload));
    }

    private readonly record struct CollectedDaemonStartProgressEntry (
        DaemonStartProgressEvent Event,
        object Payload);
}
