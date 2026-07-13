using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonPingInfoClient : IDaemonPingInfoClient
{
    private readonly Queue<object> responses = [];
    private readonly List<Invocation> invocations = [];

    private readonly TaskCompletionSource<object?> firstInvocationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RecordingDaemonPingInfoClient (params object[] responses)
    {
        foreach (var response in responses)
        {
            this.responses.Enqueue(response);
        }
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public Action? OnPingAndRead { get; set; }

    public Func<ResolvedUnityProjectContext, TimeSpan, string?, bool, CancellationToken, ValueTask<IpcUnityEditorObservation>>? PingAndReadHandler { get; set; }

    public Task WaitForFirstInvocationAsync (
        string description,
        TimeSpan timeout)
    {
        return TestAwaiter.WaitAsync(firstInvocationObserved.Task, description, timeout);
    }

    public ValueTask<IpcUnityEditorObservation> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken = default)
    {
        return RecordPingAndRead(
            unityProject,
            timeout,
            session: null,
            validateProjectFingerprint,
            cancellationToken);
    }

    public ValueTask<IpcUnityEditorObservation> PingSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return RecordPingAndRead(
            unityProject,
            timeout,
            session,
            validateProjectFingerprint,
            cancellationToken);
    }

    private ValueTask<IpcUnityEditorObservation> RecordPingAndRead (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonSession? session,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            timeout,
            session,
            validateProjectFingerprint,
            cancellationToken));
        firstInvocationObserved.TrySetResult(null);
        OnPingAndRead?.Invoke();

        if (PingAndReadHandler is not null)
        {
            return PingAndReadHandler(
                unityProject,
                timeout,
                session?.SessionToken.GetEncodedValue(),
                validateProjectFingerprint,
                cancellationToken);
        }

        if (responses.Count == 0)
        {
            throw new Xunit.Sdk.XunitException("No daemon ping response was configured.");
        }

        var response = responses.Dequeue();
        if (response is Exception exception)
        {
            throw exception;
        }

        return ValueTask.FromResult((IpcUnityEditorObservation)response);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        DaemonSession? Session,
        bool ValidateProjectFingerprint,
        CancellationToken CancellationToken)
    {
        public string? SessionToken => Session?.SessionToken.GetEncodedValue();
    }
}
