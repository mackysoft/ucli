using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Describes the latest durable progress observed for one recording lifecycle branch.</summary>
public abstract record GameViewRecordingProgress
{
    protected GameViewRecordingProgress (
        GameViewRecordingState state,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset updatedAtUtc,
        ExecutionLifecycle requiredLifecycle)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Recording progress state must be defined.");
        }
        if (GameViewRecordingExecutionContract.GetLifecycle(state) != requiredLifecycle)
        {
            throw new ArgumentException(
                "Recording progress state must belong to its payload lifecycle branch.",
                nameof(state));
        }
        if (encodedFrameCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(encodedFrameCount), encodedFrameCount, "Encoded frame count must not be negative.");
        }

        State = state;
        EffectiveMaxDurationSeconds = ContractArgumentGuard.RequirePositive(
            effectiveMaxDurationSeconds,
            nameof(effectiveMaxDurationSeconds));
        EncodedFrameCount = encodedFrameCount;
        StartedAtUtc = RequireOptionalUtc(startedAtUtc, nameof(startedAtUtc));
        StopRequestedAtUtc = RequireOptionalUtc(stopRequestedAtUtc, nameof(stopRequestedAtUtc));
        UpdatedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc));

        if ((StartedAtUtc.HasValue && StartedAtUtc > UpdatedAtUtc)
            || (StopRequestedAtUtc.HasValue && StopRequestedAtUtc > UpdatedAtUtc))
        {
            throw new ArgumentException("Recording progress timestamps must not occur after the update time.");
        }
    }

    public GameViewRecordingState State { get; }

    [UcliInt32Minimum(1)]
    public int EffectiveMaxDurationSeconds { get; }

    [UcliInt32Minimum(0)]
    public int? EncodedFrameCount { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    public DateTimeOffset? StopRequestedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    private static DateTimeOffset? RequireOptionalUtc (DateTimeOffset? value, string parameterName)
    {
        return value.HasValue
            ? ContractArgumentGuard.RequireUtcTimestamp(value.Value, parameterName)
            : null;
    }
}

/// <summary>Describes progress for a recording that can continue normal forward progress.</summary>
public sealed record GameViewRecordingActiveProgress : GameViewRecordingProgress
{
    [JsonConstructor]
    public GameViewRecordingActiveProgress (
        GameViewRecordingState state,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(
            state,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            startedAtUtc,
            stopRequestedAtUtc,
            updatedAtUtc,
            ExecutionLifecycle.Active)
    {
    }
}

/// <summary>Describes progress for a recording being finalized or recovered.</summary>
public sealed record GameViewRecordingRecoveryProgress : GameViewRecordingProgress
{
    [JsonConstructor]
    public GameViewRecordingRecoveryProgress (
        GameViewRecordingState state,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(
            state,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            startedAtUtc,
            stopRequestedAtUtc,
            updatedAtUtc,
            ExecutionLifecycle.Recovery)
    {
    }
}

/// <summary>Describes progress for a recording with an immutable terminal result.</summary>
public sealed record GameViewRecordingTerminalProgress : GameViewRecordingProgress
{
    [JsonConstructor]
    public GameViewRecordingTerminalProgress (
        GameViewRecordingState state,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(
            state,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            startedAtUtc,
            stopRequestedAtUtc,
            updatedAtUtc,
            ExecutionLifecycle.Terminal)
    {
    }
}

/// <summary>Summarizes a terminal recording outcome.</summary>
public sealed record GameViewRecordingTerminalSummary
{
    [JsonConstructor]
    public GameViewRecordingTerminalSummary (
        GameViewRecordingState state,
        GameViewRecordingStopReason stopReason,
        GameViewRecordingVideoDisposition videoDisposition,
        GameViewRecordingCleanupDisposition cleanupDisposition,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        if (GameViewRecordingExecutionContract.GetLifecycle(state) != ExecutionLifecycle.Terminal)
        {
            throw new ArgumentException("Terminal summary state must be completed, failed, or indeterminate.", nameof(state));
        }
        if (!TextVocabulary.IsDefined(stopReason))
        {
            throw new ArgumentOutOfRangeException(nameof(stopReason), stopReason, "Recording stop reason must be defined.");
        }
        if (!TextVocabulary.IsDefined(videoDisposition))
        {
            throw new ArgumentOutOfRangeException(nameof(videoDisposition), videoDisposition, "Video disposition must be defined.");
        }
        if (!TextVocabulary.IsDefined(cleanupDisposition))
        {
            throw new ArgumentOutOfRangeException(nameof(cleanupDisposition), cleanupDisposition, "Cleanup disposition must be defined.");
        }

        State = state;
        StopReason = stopReason;
        VideoDisposition = videoDisposition;
        CleanupDisposition = cleanupDisposition;
        StartedAtUtc = startedAtUtc.HasValue
            ? ContractArgumentGuard.RequireUtcTimestamp(startedAtUtc.Value, nameof(startedAtUtc))
            : null;
        CompletedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(completedAtUtc, nameof(completedAtUtc));

        if (state is GameViewRecordingState.Completed or GameViewRecordingState.Failed
            && !StartedAtUtc.HasValue)
        {
            throw new ArgumentException("A completed or failed recording must carry its accepted start time.", nameof(startedAtUtc));
        }
        if (StartedAtUtc.HasValue && CompletedAtUtc < StartedAtUtc.Value)
        {
            throw new ArgumentException("Recording completion time must not precede its start time.", nameof(completedAtUtc));
        }
        if (state == GameViewRecordingState.Completed
            && (videoDisposition != GameViewRecordingVideoDisposition.Available
                || cleanupDisposition != GameViewRecordingCleanupDisposition.Complete))
        {
            throw new ArgumentException("A completed recording requires an available video and complete cleanup.");
        }
        if (state == GameViewRecordingState.Failed
            && (videoDisposition == GameViewRecordingVideoDisposition.Unconfirmed
                || cleanupDisposition == GameViewRecordingCleanupDisposition.Unconfirmed
                || stopReason == GameViewRecordingStopReason.Unconfirmed))
        {
            throw new ArgumentException("A failed recording must not retain unconfirmed terminal facts.");
        }
        if (state != GameViewRecordingState.Indeterminate
            && stopReason == GameViewRecordingStopReason.Unconfirmed)
        {
            throw new ArgumentException("Only an indeterminate recording may have an unconfirmed stop reason.", nameof(stopReason));
        }
    }

    public GameViewRecordingState State { get; }

    public GameViewRecordingStopReason StopReason { get; }

    public GameViewRecordingVideoDisposition VideoDisposition { get; }

    public GameViewRecordingCleanupDisposition CleanupDisposition { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }
}
