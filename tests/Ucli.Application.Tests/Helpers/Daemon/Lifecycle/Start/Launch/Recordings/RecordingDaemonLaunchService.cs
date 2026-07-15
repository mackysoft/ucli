using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonLaunchService : IDaemonLaunchService
{
    private readonly List<Invocation> invocations = [];

    public DaemonStartResult NextResult { get; set; } =
        DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 9090), IpcUnityEditorObservationTestFactory.Create());

    public Func<ResolvedUnityProjectContext, ExecutionDeadline, DaemonEditorMode, DaemonStartupBlockedProcessPolicy, IDaemonStartProgressObserver?, CancellationToken, ValueTask<DaemonStartResult>>? Handler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStartResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonEditorMode editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(unityProject, deadline, editorMode, onStartupBlocked, progressObserver, cancellationToken));
        if (Handler is not null)
        {
            return Handler(unityProject, deadline, editorMode, onStartupBlocked, progressObserver, cancellationToken);
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        ExecutionDeadline Deadline,
        DaemonEditorMode EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        IDaemonStartProgressObserver? ProgressObserver,
        CancellationToken CancellationToken);
}
