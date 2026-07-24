using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonGuiStartupObserver : IDaemonGuiStartupObserver
{
    private readonly List<Invocation> invocations = [];

    public DaemonGuiStartupObservationResult NextResult { get; set; } =
        DaemonGuiStartupObservationResult.Success(DaemonSessionTestFactory.Create(
            processId: 2000,
            sessionToken: "session-token",
            endpointAddress: "ucli-daemon-test-endpoint"),
            IpcUnityEditorObservationTestFactory.Create(IpcEditorLifecycleState.Ready));

    public Func<CancellationToken, ValueTask<DaemonGuiStartupObservationResult>>? Handler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonGuiStartupObservationResult> WaitForStartupAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        AbsolutePath unityLogPath,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(unityProject, processId, processStartedAtUtc, unityLogPath, deadline, cancellationToken));
        if (Handler is not null)
        {
            return Handler(cancellationToken);
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        int ProcessId,
        DateTimeOffset ProcessStartedAtUtc,
        AbsolutePath UnityLogPath,
        ExecutionDeadline Deadline,
        CancellationToken CancellationToken);
}
