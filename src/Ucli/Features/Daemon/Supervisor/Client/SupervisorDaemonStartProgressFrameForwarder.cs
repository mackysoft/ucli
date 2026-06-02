using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Client;

/// <summary> Reprojects supervisor daemon-start progress frames into caller-visible command progress entries. </summary>
internal sealed class SupervisorDaemonStartProgressFrameForwarder
{
    private readonly ICommandProgressSink progressSink;
    private readonly string projectFingerprint;
    private readonly int timeoutMilliseconds;
    private readonly string? editorMode;
    private readonly string onStartupBlocked;

    /// <summary> Initializes a new instance of the <see cref="SupervisorDaemonStartProgressFrameForwarder" /> class. </summary>
    public SupervisorDaemonStartProgressFrameForwarder (
        ICommandProgressSink progressSink,
        string projectFingerprint,
        int timeoutMilliseconds,
        string? editorMode,
        string onStartupBlocked)
    {
        this.progressSink = progressSink ?? throw new ArgumentNullException(nameof(progressSink));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMilliseconds);
        ArgumentException.ThrowIfNullOrWhiteSpace(onStartupBlocked);

        this.projectFingerprint = projectFingerprint;
        this.timeoutMilliseconds = timeoutMilliseconds;
        this.editorMode = editorMode;
        this.onStartupBlocked = onStartupBlocked;
    }

    /// <summary> Forwards one valid supervisor progress frame to the caller progress sink. </summary>
    public async ValueTask ForwardAsync (
        IpcStreamFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (string.IsNullOrWhiteSpace(frame.Event))
            {
                return;
            }

            if (!ContractLiteralCodec.TryParse<DaemonStartProgressEvent>(frame.Event, out var progressEvent))
            {
                return;
            }

            if (!DaemonStartProgressPayloadContract.TryGetPayloadKind(progressEvent, out var payloadKind))
            {
                return;
            }

            switch (payloadKind)
            {
                case DaemonStartProgressPayloadKind.StartupObservation:
                    await ForwardStartupObservationAsync(frame.Event, frame.Payload, cancellationToken).ConfigureAwait(false);
                    return;
                case DaemonStartProgressPayloadKind.LifecycleSnapshot:
                    await ForwardLifecycleSnapshotAsync(frame.Event, frame.Payload, cancellationToken).ConfigureAwait(false);
                    return;
                default:
                    return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // NOTE: Supervisor progress is a non-authoritative observation stream; terminal responses remain authoritative.
        }
    }

    private ValueTask ForwardStartupObservationAsync (
        string eventName,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var entry = payload.Deserialize<DaemonStartStartupObservationProgressEntry>(IpcJsonSerializerOptions.Default);
        if (entry is null || !IsValidStartupObservation(entry))
        {
            return ValueTask.CompletedTask;
        }

        return progressSink.OnEntryAsync(eventName, entry, cancellationToken);
    }

    private ValueTask ForwardLifecycleSnapshotAsync (
        string eventName,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var entry = payload.Deserialize<DaemonStartLifecycleSnapshotProgressEntry>(IpcJsonSerializerOptions.Default);
        if (entry is null || !IsValidLifecycleSnapshot(entry))
        {
            return ValueTask.CompletedTask;
        }

        return progressSink.OnEntryAsync(eventName, entry, cancellationToken);
    }

    private bool IsValidStartupObservation (DaemonStartStartupObservationProgressEntry entry)
    {
        return HasExpectedEnvelope(entry.PayloadKind, DaemonStartProgressPayloadKind.StartupObservation, entry.ProjectFingerprint, entry.TimeoutMilliseconds, entry.EditorMode, entry.OnStartupBlocked)
            && IsOptionalOwnerKind(entry.OwnerKind)
            && IsOptionalStartupStatus(entry.StartupStatus)
            && IsOptionalStartupBlockingReason(entry.StartupBlockingReason)
            && IsOptionalStartupPhase(entry.StartupPhase)
            && IsOptionalRetryDisposition(entry.RetryDisposition)
            && !IsBlankWhenPresent(entry.LaunchAttemptId)
            && !IsBlankWhenPresent(entry.Message)
            && !IsBlankWhenPresent(entry.ErrorCode);
    }

    private bool IsValidLifecycleSnapshot (DaemonStartLifecycleSnapshotProgressEntry entry)
    {
        return HasExpectedEnvelope(entry.PayloadKind, DaemonStartProgressPayloadKind.LifecycleSnapshot, entry.ProjectFingerprint, entry.TimeoutMilliseconds, entry.EditorMode, entry.OnStartupBlocked)
            && IpcEditorLifecycleStateCodec.TryParse(entry.LifecycleState, out var lifecycleState)
            && string.Equals(entry.LifecycleState, lifecycleState, StringComparison.Ordinal)
            && IsOptionalBlockingReason(entry.BlockingReason);
    }

    private bool HasExpectedEnvelope (
        string payloadKind,
        DaemonStartProgressPayloadKind expectedPayloadKind,
        string entryProjectFingerprint,
        int entryTimeoutMilliseconds,
        string? entryEditorMode,
        string entryOnStartupBlocked)
    {
        return string.Equals(payloadKind, ContractLiteralCodec.ToValue(expectedPayloadKind), StringComparison.Ordinal)
            && string.Equals(entryProjectFingerprint, projectFingerprint, StringComparison.Ordinal)
            && entryTimeoutMilliseconds == timeoutMilliseconds
            && string.Equals(entryOnStartupBlocked, onStartupBlocked, StringComparison.Ordinal)
            && IsOptionalEditorMode(entryEditorMode)
            && (editorMode is null || string.Equals(entryEditorMode, editorMode, StringComparison.Ordinal));
    }

    private static bool IsOptionalEditorMode (string? value)
    {
        return value is null || ContractLiteralCodec.TryParse<DaemonEditorMode>(value, out _);
    }

    private static bool IsOptionalOwnerKind (string? value)
    {
        return value is null || ContractLiteralCodec.TryParse<DaemonSessionOwnerKind>(value, out _);
    }

    private static bool IsOptionalStartupStatus (string? value)
    {
        return value is null
            || value is DaemonStartupStatusValues.Launching
                or DaemonStartupStatusValues.WaitingForEndpoint
                or DaemonStartupStatusValues.Blocked
                or DaemonStartupStatusValues.Timeout
                or DaemonStartupStatusValues.Failed
                or DaemonStartupStatusValues.Completed;
    }

    private static bool IsOptionalStartupBlockingReason (string? value)
    {
        return value is null
            || value is DaemonStartupBlockingReasonValues.SafeMode
                or DaemonStartupBlockingReasonValues.Compile
                or DaemonStartupBlockingReasonValues.PackageResolution
                or DaemonStartupBlockingReasonValues.UcliPlugin
                or DaemonStartupBlockingReasonValues.PrecompiledAssemblyConflict
                or DaemonStartupBlockingReasonValues.ModalDialog
                or DaemonStartupBlockingReasonValues.EndpointNotRegistered
                or DaemonStartupBlockingReasonValues.ProcessExit
                or DaemonStartupBlockingReasonValues.Unknown;
    }

    private static bool IsOptionalStartupPhase (string? value)
    {
        return value is null || DaemonDiagnosisStartupPhaseValues.IsSupported(value);
    }

    private static bool IsOptionalRetryDisposition (string? value)
    {
        return value is null
            || value is DaemonStartupRetryDispositionValues.RetryImmediately
                or DaemonStartupRetryDispositionValues.WaitThenRetry
                or DaemonStartupRetryDispositionValues.RetryAfterFix
                or DaemonStartupRetryDispositionValues.ManualActionRequired
                or DaemonStartupRetryDispositionValues.DoNotRetry
                or DaemonStartupRetryDispositionValues.Unknown;
    }

    private static bool IsOptionalBlockingReason (string? value)
    {
        if (value is null)
        {
            return true;
        }

        return IpcEditorBlockingReasonCodec.TryParse(value, out var blockingReason)
            && string.Equals(value, blockingReason, StringComparison.Ordinal);
    }

    private static bool IsBlankWhenPresent (string? value)
    {
        return value is not null && string.IsNullOrWhiteSpace(value);
    }
}
