using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonProjectLifecycleGateway : IDaemonProjectLifecycleGateway
{
    private readonly List<EnsureRunningInvocation> ensureRunningInvocations = [];
    private readonly List<TryStopProjectInvocation> tryStopProjectInvocations = [];

    public DaemonStartResult EnsureRunningResult { get; set; } = DaemonStartResult.Failure(
        ExecutionError.InternalError("No daemon start result is configured."));

    public DaemonStopResult? TryStopProjectResult { get; set; }

    public Func<ResolvedUnityProjectContext, TimeSpan, DaemonEditorMode?, DaemonStartupBlockedProcessPolicy, IDaemonProjectLifecycleProgressObserver?, ICommandProgressSink?, CancellationToken, ValueTask<DaemonStartResult>>? EnsureRunningHandler { get; set; }

    public Func<ResolvedUnityProjectContext, TimeSpan, CancellationToken, ValueTask<DaemonStopResult?>>? TryStopProjectHandler { get; set; }

    public IReadOnlyList<EnsureRunningInvocation> EnsureRunningInvocations => ensureRunningInvocations;

    public IReadOnlyList<TryStopProjectInvocation> TryStopProjectInvocations => tryStopProjectInvocations;

    public ValueTask<DaemonStartResult> EnsureRunningAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonProjectLifecycleProgressObserver? progressObserver = null,
        ICommandProgressSink? supervisorProgressSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        ensureRunningInvocations.Add(new EnsureRunningInvocation(
            unityProject,
            timeout,
            editorMode,
            onStartupBlocked,
            progressObserver,
            supervisorProgressSink,
            cancellationToken));

        if (EnsureRunningHandler != null)
        {
            return EnsureRunningHandler(
                unityProject,
                timeout,
                editorMode,
                onStartupBlocked,
                progressObserver,
                supervisorProgressSink,
                cancellationToken);
        }

        return ValueTask.FromResult(EnsureRunningResult);
    }

    public ValueTask<DaemonStopResult?> TryStopProjectAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        tryStopProjectInvocations.Add(new TryStopProjectInvocation(
            unityProject,
            timeout,
            cancellationToken));

        if (TryStopProjectHandler != null)
        {
            return TryStopProjectHandler(unityProject, timeout, cancellationToken);
        }

        return ValueTask.FromResult(TryStopProjectResult);
    }

    internal readonly record struct EnsureRunningInvocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        IDaemonProjectLifecycleProgressObserver? ProgressObserver,
        ICommandProgressSink? SupervisorProgressSink,
        CancellationToken CancellationToken);

    internal readonly record struct TryStopProjectInvocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
