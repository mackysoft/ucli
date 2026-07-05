using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonGuiEditorAttachService : IDaemonGuiEditorAttachService
{
    private readonly List<Invocation> invocations = [];

    public DaemonStartResult? NextResult { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStartResult?> TryAttachExistingGuiEditorAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(unityProject, timeout, editorMode, onStartupBlocked, progressObserver, cancellationToken));
        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        IDaemonStartProgressObserver? ProgressObserver,
        CancellationToken CancellationToken);
}
