using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonExistingSessionGateService : IDaemonExistingSessionGateService
{
    private readonly List<Invocation> invocations = [];

    public DaemonStartResult? NextResult { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStartResult?> TryHandleExistingSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(unityProject, session, timeout, editorMode, progressObserver, cancellationToken));
        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        TimeSpan Timeout,
        DaemonEditorMode? EditorMode,
        IDaemonStartProgressObserver? ProgressObserver,
        CancellationToken CancellationToken);
}
