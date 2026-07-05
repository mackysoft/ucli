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

    public Func<ResolvedUnityProjectContext, TimeSpan, CancellationToken, ValueTask<DaemonStopResult>>? StopHandler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStopResult> StopAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(unityProject, timeout, cancellationToken));
        if (StopHandler is not null)
        {
            return StopHandler(unityProject, timeout, cancellationToken);
        }

        return ValueTask.FromResult(StopResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
