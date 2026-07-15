using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonGuiSessionRegistrationAwaiter : IDaemonGuiSessionRegistrationAwaiter
{
    private readonly List<Invocation> invocations = [];
    private Action<int>? onWait;

    public DaemonGuiSessionRegistrationWaitResult Result { get; set; } =
        DaemonGuiSessionRegistrationWaitResult.Success(CreateDefaultSession(), IpcUnityEditorObservationTestFactory.Create());

    public DaemonGuiSessionRegistrationWaitResult NextResult
    {
        get => Result;
        set => Result = value;
    }

    public Queue<DaemonGuiSessionRegistrationWaitResult> Results { get; } = [];

    public Action? OnWaitForSession { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public void AdvanceTimeOnFirstWait (
        ManualTimeProvider timeProvider,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        onWait = waitNumber =>
        {
            if (waitNumber == 1)
            {
                timeProvider.Advance(elapsed);
            }
        };
    }

    public ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        ExecutionDeadline deadline,
        DateTimeOffset? expectedProcessStartedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        invocations.Add(new Invocation(
            unityProject,
            expectedProcessId,
            deadline,
            remainingTimeout,
            expectedProcessStartedAtUtc,
            cancellationToken));
        OnWaitForSession?.Invoke();
        onWait?.Invoke(invocations.Count);
        return ValueTask.FromResult(Results.Count > 0 ? Results.Dequeue() : Result);
    }

    private static DaemonSession CreateDefaultSession ()
    {
        return DaemonSessionTestFactory.Create();
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        int ExpectedProcessId,
        ExecutionDeadline Deadline,
        TimeSpan RemainingTimeout,
        DateTimeOffset? ExpectedProcessStartedAtUtc,
        CancellationToken CancellationToken);
}
