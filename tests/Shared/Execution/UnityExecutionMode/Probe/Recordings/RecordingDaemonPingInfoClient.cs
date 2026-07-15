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

    public Func<ResolvedUnityProjectContext, TimeSpan, bool, CancellationToken, ValueTask<IpcUnityEditorObservation>>? PingAndReadHandler { get; set; }

    public Func<ResolvedUnityProjectContext, DaemonSession, ExecutionDeadline, bool, CancellationToken, ValueTask<IpcUnityEditorObservation>>? PingSessionAndReadHandler { get; set; }

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
            deadline: null,
            session: null,
            requestId: null,
            validateProjectFingerprint,
            cancellationToken);
    }

    public ValueTask<IpcUnityEditorObservation> PingSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        Guid requestId,
        ExecutionDeadline deadline,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(deadline);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Daemon ping request identifier must not be empty.", nameof(requestId));
        }

        if (!deadline.TryGetRemainingTimeout(out var timeout))
        {
            throw new TimeoutException("Timed out before recording the exact-session daemon ping.");
        }

        return RecordPingAndRead(
            unityProject,
            timeout,
            deadline,
            session,
            requestId,
            validateProjectFingerprint,
            cancellationToken);
    }

    private ValueTask<IpcUnityEditorObservation> RecordPingAndRead (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        ExecutionDeadline? deadline,
        DaemonSession? session,
        Guid? requestId,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            timeout,
            deadline,
            session,
            requestId,
            validateProjectFingerprint,
            cancellationToken));
        firstInvocationObserved.TrySetResult(null);
        OnPingAndRead?.Invoke();

        if (session is null && PingAndReadHandler is not null)
        {
            return PingAndReadHandler(
                unityProject,
                timeout,
                validateProjectFingerprint,
                cancellationToken);
        }

        if (session is not null && PingSessionAndReadHandler is not null)
        {
            return PingSessionAndReadHandler(
                unityProject,
                session,
                deadline!,
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
        ExecutionDeadline? Deadline,
        DaemonSession? Session,
        Guid? RequestId,
        bool ValidateProjectFingerprint,
        CancellationToken CancellationToken)
    {
        public string? SessionToken => Session?.SessionToken.GetEncodedValue();
    }
}
