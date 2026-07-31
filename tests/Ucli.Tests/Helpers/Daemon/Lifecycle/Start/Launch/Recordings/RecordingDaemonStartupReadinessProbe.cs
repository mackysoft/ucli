using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
{
    private readonly List<Invocation> invocations = [];

    public DaemonStartupReadinessProbeResult NextResult { get; set; } =
        DaemonStartupReadinessProbeResult.Ready(
            UnityEditorObservationTestFactory.Create(UnityEditorLifecycleState.Ready));

    public Action? OnWaitUntilReady { get; set; }

    public Exception? NextException { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReadyAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        int? daemonProcessId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnWaitUntilReady?.Invoke();
        invocations.Add(new Invocation(
            unityProject,
            deadline,
            daemonProcessId,
            cancellationToken));
        if (NextException is not null)
        {
            return ValueTask.FromException<DaemonStartupReadinessProbeResult>(NextException);
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        ExecutionDeadline Deadline,
        int? DaemonProcessId,
        CancellationToken CancellationToken);
}
