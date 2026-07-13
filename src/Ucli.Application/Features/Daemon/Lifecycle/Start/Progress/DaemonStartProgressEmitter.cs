using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

/// <summary> Emits host-visible daemon-start progress entries without owning daemon-start decisions. </summary>
internal sealed class DaemonStartProgressEmitter :
    IDaemonProjectLifecycleProgressObserver,
    IDaemonStartProgressObserver
{
    private readonly ICommandProgressSink progressSink;
    private readonly ProjectFingerprint projectFingerprint;
    private readonly int timeoutMilliseconds;
    private readonly string? editorMode;
    private readonly string onStartupBlocked;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartProgressEmitter" /> class. </summary>
    public DaemonStartProgressEmitter (
        ICommandProgressSink? progressSink,
        ProjectFingerprint projectFingerprint,
        int timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        ArgumentNullException.ThrowIfNull(projectFingerprint);
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
        return EmitAsync(DaemonStartProgressEvent.Started, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the plugin-verification start entry. </summary>
    public ValueTask EmitPluginVerificationStartedAsync (CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.PluginVerificationStarted, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the plugin-verification completion entry. </summary>
    public ValueTask EmitPluginVerificationCompletedAsync (
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        return EmitCompletedAsync(DaemonStartProgressEvent.PluginVerificationCompleted, error, cancellationToken);
    }

    /// <summary> Emits the supervisor-bootstrap start entry. </summary>
    public ValueTask EmitSupervisorBootstrapStartedAsync (CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.SupervisorBootstrapStarted, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the supervisor-bootstrap completion entry. </summary>
    public ValueTask EmitSupervisorBootstrapCompletedAsync (
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        return EmitCompletedAsync(DaemonStartProgressEvent.SupervisorBootstrapCompleted, error, cancellationToken);
    }

    /// <summary> Emits the supervisor ensureRunning request start entry. </summary>
    public ValueTask EmitEnsureRunningStartedAsync (CancellationToken cancellationToken)
    {
        return EmitAsync(DaemonStartProgressEvent.EnsureRunningStarted, result: null, startStatus: null, daemonStatus: null, error: null, cancellationToken);
    }

    /// <summary> Emits the supervisor ensureRunning request completion entry. </summary>
    public ValueTask EmitEnsureRunningCompletedAsync (
        DaemonStartResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EmitAsync(
            DaemonStartProgressEvent.EnsureRunningCompleted,
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
            DaemonStartProgressEvent.Completed,
            ResolveResult(error),
            startStatus,
            daemonStatus,
            error,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask EmitLaunchingAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        return EmitStartupObservationAsync(DaemonStartProgressEvent.Launching, observation, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask EmitWaitingForEndpointAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        return EmitStartupObservationAsync(DaemonStartProgressEvent.WaitingForEndpoint, observation, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask EmitBlockerDetectedAsync (
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        return EmitStartupObservationAsync(DaemonStartProgressEvent.BlockerDetected, observation, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask EmitSessionRegisteredAsync (
        DaemonSession session,
        string? launchAttemptId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return EmitStartupObservationAsync(
            DaemonStartProgressEvent.SessionRegistered,
            CreateSessionObservation(session, launchAttemptId),
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask EmitEndpointRegisteredAsync (
        DaemonSession session,
        string? launchAttemptId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return EmitStartupObservationAsync(
            DaemonStartProgressEvent.EndpointRegistered,
            CreateSessionObservation(session, launchAttemptId),
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask EmitLifecycleObservedAsync (
        DaemonStartLifecycleSnapshot lifecycleSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lifecycleSnapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = new DaemonStartLifecycleSnapshotProgressEntry(
            PayloadKind: ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot),
            ProjectFingerprint: projectFingerprint,
            TimeoutMilliseconds: timeoutMilliseconds,
            EditorMode: editorMode,
            OnStartupBlocked: onStartupBlocked,
            LifecycleState: lifecycleSnapshot.LifecycleState,
            BlockingReason: lifecycleSnapshot.BlockingReason,
            CanAcceptExecutionRequests: lifecycleSnapshot.CanAcceptExecutionRequests);
        return progressSink.OnEntryAsync(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved), entry, cancellationToken);
    }

    private ValueTask EmitCompletedAsync (
        DaemonStartProgressEvent progressEvent,
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        return EmitAsync(progressEvent, ResolveResult(error), startStatus: null, daemonStatus: null, error, cancellationToken);
    }

    private ValueTask EmitAsync (
        DaemonStartProgressEvent progressEvent,
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
        return progressSink.OnEntryAsync(ContractLiteralCodec.ToValue(progressEvent), entry, cancellationToken);
    }

    private ValueTask EmitStartupObservationAsync (
        DaemonStartProgressEvent progressEvent,
        DaemonStartStartupProgressObservation observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!DaemonStartProgressPayloadContract.IsStartupObservation(progressEvent))
        {
            throw new InvalidOperationException(
                $"Daemon-start progress event does not carry a startup-observation payload: {progressEvent}.");
        }

        var entry = new DaemonStartStartupObservationProgressEntry(
            PayloadKind: ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
            ProjectFingerprint: projectFingerprint,
            TimeoutMilliseconds: timeoutMilliseconds,
            EditorMode: observation.EditorMode ?? editorMode,
            OnStartupBlocked: onStartupBlocked,
            LaunchAttemptId: observation.LaunchAttemptId,
            OwnerKind: observation.OwnerKind,
            CanShutdownProcess: observation.CanShutdownProcess,
            ProcessId: observation.ProcessId,
            ProcessStartedAtUtc: observation.ProcessStartedAtUtc,
            StartupStatus: observation.StartupStatus,
            StartupBlockingReason: observation.StartupBlockingReason,
            StartupPhase: observation.StartupPhase,
            RetryDisposition: observation.RetryDisposition,
            Message: observation.Message,
            ErrorCode: observation.ErrorCode);
        return progressSink.OnEntryAsync(ContractLiteralCodec.ToValue(progressEvent), entry, cancellationToken);
    }

    private static DaemonStartStartupProgressObservation CreateSessionObservation (
        DaemonSession session,
        string? launchAttemptId)
    {
        return new DaemonStartStartupProgressObservation(
            LaunchAttemptId: launchAttemptId,
            EditorMode: ContractLiteralCodec.ToValue(session.EditorMode),
            OwnerKind: ContractLiteralCodec.ToValue(session.OwnerKind),
            CanShutdownProcess: session.CanShutdownProcess,
            ProcessId: session.ProcessId,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
            StartupStatus: null,
            StartupBlockingReason: null,
            StartupPhase: null,
            RetryDisposition: null,
            Message: null,
            ErrorCode: null);
    }

    private static string ResolveResult (ExecutionError? error)
    {
        return error is null
            ? ContractLiteralCodec.ToValue(CommandProgressResult.Succeeded)
            : ContractLiteralCodec.ToValue(CommandProgressResult.Failed);
    }
}
