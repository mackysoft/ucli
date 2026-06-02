using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

/// <summary> Emits host-visible daemon-start progress entries without owning daemon-start decisions. </summary>
internal sealed class DaemonStartProgressEmitter : IDaemonProjectLifecycleProgressObserver
{
    private readonly ICommandProgressSink progressSink;
    private readonly string projectFingerprint;
    private readonly int timeoutMilliseconds;
    private readonly string? editorMode;
    private readonly string onStartupBlocked;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartProgressEmitter" /> class. </summary>
    public DaemonStartProgressEmitter (
        ICommandProgressSink? progressSink,
        string projectFingerprint,
        int timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMilliseconds);

        this.progressSink = progressSink ?? NullCommandProgressSink.Instance;
        this.projectFingerprint = projectFingerprint;
        this.timeoutMilliseconds = timeoutMilliseconds;
        this.editorMode = editorMode.HasValue
            ? ContractLiteralCodec.ToValue(editorMode.Value)
            : null;
        this.onStartupBlocked = ContractLiteralCodec.ToValue(onStartupBlocked);
    }

    /// <summary> Emits the daemon-start workflow start entry. </summary>
    public ValueTask EmitStartedAsync (CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEventNames.Started, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the plugin-verification start entry. </summary>
    public ValueTask EmitPluginVerificationStartedAsync (CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEventNames.PluginVerificationStarted, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the plugin-verification completion entry. </summary>
    public ValueTask EmitPluginVerificationCompletedAsync (
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        return EmitCompletedAsync(DaemonStartProgressEventNames.PluginVerificationCompleted, error, cancellationToken);
    }

    /// <summary> Emits the supervisor-bootstrap start entry. </summary>
    public ValueTask EmitSupervisorBootstrapStartedAsync (CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEventNames.SupervisorBootstrapStarted, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the supervisor-bootstrap completion entry. </summary>
    public ValueTask EmitSupervisorBootstrapCompletedAsync (
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        return EmitCompletedAsync(DaemonStartProgressEventNames.SupervisorBootstrapCompleted, error, cancellationToken);
    }

    /// <summary> Emits the supervisor ensureRunning request start entry. </summary>
    public ValueTask EmitEnsureRunningStartedAsync (CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEventNames.EnsureRunningStarted, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the supervisor ensureRunning request completion entry. </summary>
    public ValueTask EmitEnsureRunningCompletedAsync (
        DaemonStartResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EmitAsync(
            DaemonStartProgressEventNames.EnsureRunningCompleted,
            ResolveResult(result.Error),
            result.Status,
            result.DaemonStatus,
            result.Error,
            cancellationToken);
    }

    /// <summary> Emits the daemon-start workflow completion entry. </summary>
    public ValueTask EmitCompletedAsync (
        DaemonStartStatus startStatus,
        DaemonStatusKind? daemonStatus,
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        return EmitAsync(
            DaemonStartProgressEventNames.Completed,
            ResolveResult(error),
            startStatus,
            daemonStatus,
            error,
            cancellationToken);
    }

    private ValueTask EmitCompletedAsync (
        string eventName,
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        return EmitAsync(eventName, ResolveResult(error), startStatus: null, daemonStatus: null, error, cancellationToken);
    }

    private ValueTask EmitAsync (
        string eventName,
        string? result,
        DaemonStartStatus? startStatus,
        DaemonStatusKind? daemonStatus,
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entry = new DaemonStartProgressEntry(
            ProjectFingerprint: projectFingerprint,
            TimeoutMilliseconds: timeoutMilliseconds,
            EditorMode: editorMode,
            OnStartupBlocked: onStartupBlocked,
            Result: result,
            StartStatus: startStatus.HasValue ? ContractLiteralCodec.ToValue(startStatus.Value) : null,
            DaemonStatus: daemonStatus.HasValue ? ContractLiteralCodec.ToValue(daemonStatus.Value) : null,
            ErrorCode: error is null ? null : ExecutionErrorCodeMapper.ToCode(error).Value);
        return progressSink.OnEntryAsync(eventName, entry, cancellationToken);
    }

    private static string ResolveResult (ExecutionError? error)
    {
        return error is null
            ? DaemonStartProgressResultValues.Succeeded
            : DaemonStartProgressResultValues.Failed;
    }
}
