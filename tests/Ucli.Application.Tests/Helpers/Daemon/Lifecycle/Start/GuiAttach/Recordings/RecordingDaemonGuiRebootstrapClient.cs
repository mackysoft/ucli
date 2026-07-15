namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonGuiRebootstrapClient : IDaemonGuiRebootstrapClient
{
    private readonly List<Invocation> invocations = [];

    public DaemonGuiRebootstrapRequestResult Result { get; set; } =
        DaemonGuiRebootstrapRequestResult.Accepted();

    public Action? OnRequest { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonGuiRebootstrapRequestResult> RequestRebootstrapAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);
        invocations.Add(new Invocation(
            unityProject,
            expectedProcessId,
            expectedProcessStartedAtUtc,
            deadline,
            remainingTimeout,
            cancellationToken));
        OnRequest?.Invoke();
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        int ExpectedProcessId,
        DateTimeOffset? ExpectedProcessStartedAtUtc,
        ExecutionDeadline Deadline,
        TimeSpan RemainingTimeout,
        CancellationToken CancellationToken);
}
