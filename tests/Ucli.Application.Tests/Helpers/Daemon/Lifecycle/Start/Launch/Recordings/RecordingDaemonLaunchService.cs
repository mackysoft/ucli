using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonLaunchService : IDaemonLaunchService
{
    private readonly List<Invocation> invocations = [];

    public DaemonStartResult NextResult { get; set; } =
        DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 9090), UnityEditorObservationTestFactory.Create());

    public Func<ResolvedUnityProjectContext, ExecutionDeadline, UnityEditorMode, DaemonStartupBlockedProcessPolicy, IDaemonStartProgressObserver?, CancellationToken, ValueTask<DaemonStartResult>>? Handler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStartResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        UnityEditorMode editorMode,
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
        UnityEditorMode EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        IDaemonStartProgressObserver? ProgressObserver,
        CancellationToken CancellationToken);
}
