using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonStopOperation : IDaemonStopOperation
{
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonStopOperation ()
    {
    }

    public RecordingDaemonStopOperation (DaemonStopResult result)
    {
        StopResult = result ?? throw new ArgumentNullException(nameof(result));
    }

    public DaemonStopResult StopResult { get; set; } = DaemonStopResult.Stopped();

    public Func<ResolvedUnityProjectContext, ExecutionDeadline, CancellationToken, ValueTask<DaemonStopResult>>? StopHandler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStopResult> StopAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        invocations.Add(new Invocation(unityProject, deadline, remainingTimeout, cancellationToken));
        if (StopHandler is not null)
        {
            return StopHandler(unityProject, deadline, cancellationToken);
        }

        return ValueTask.FromResult(StopResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        ExecutionDeadline Deadline,
        TimeSpan RemainingTimeout,
        CancellationToken CancellationToken);
}
