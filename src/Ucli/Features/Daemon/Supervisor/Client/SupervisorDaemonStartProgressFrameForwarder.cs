using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Client;

/// <summary> Reprojects supervisor daemon-start progress frames into caller-visible command progress entries. </summary>
internal sealed class SupervisorDaemonStartProgressFrameForwarder
{
    private readonly ICommandProgressSink progressSink;
    private readonly ProjectFingerprint projectFingerprint;
    private readonly int timeoutMilliseconds;
    private readonly DaemonEditorMode? editorMode;
    private readonly DaemonStartupBlockedProcessPolicy onStartupBlocked;

    /// <summary> Initializes a new instance of the <see cref="SupervisorDaemonStartProgressFrameForwarder" /> class. </summary>
    public SupervisorDaemonStartProgressFrameForwarder (
        ICommandProgressSink progressSink,
        ProjectFingerprint projectFingerprint,
        int timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        this.progressSink = progressSink ?? throw new ArgumentNullException(nameof(progressSink));
        ArgumentNullException.ThrowIfNull(projectFingerprint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMilliseconds);

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
            && IsOptionalContractLiteral<DaemonSessionOwnerKind>(entry.OwnerKind)
            && IsOptionalContractLiteral<DaemonStartupStatus>(entry.StartupStatus)
            && IsOptionalContractLiteral<DaemonStartupBlockingReason>(entry.StartupBlockingReason)
            && IsOptionalContractLiteral<DaemonDiagnosisStartupPhase>(entry.StartupPhase)
            && IsOptionalContractLiteral<DaemonStartupRetryDisposition>(entry.RetryDisposition)
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
        ProjectFingerprint entryProjectFingerprint,
        int entryTimeoutMilliseconds,
        string? entryEditorMode,
        string entryOnStartupBlocked)
    {
        return ContractLiteralCodec.Matches(payloadKind, expectedPayloadKind)
            && entryProjectFingerprint == projectFingerprint
            && entryTimeoutMilliseconds > 0
            && entryTimeoutMilliseconds <= timeoutMilliseconds
            && ContractLiteralCodec.Matches(entryOnStartupBlocked, onStartupBlocked)
            && IsOptionalContractLiteral<DaemonEditorMode>(entryEditorMode)
            && (!editorMode.HasValue || ContractLiteralCodec.Matches(entryEditorMode, editorMode.Value));
    }

    private static bool IsOptionalContractLiteral<TEnum> (string? value)
        where TEnum : struct, Enum
    {
        return value is null || ContractLiteralCodec.IsDefined<TEnum>(value);
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
