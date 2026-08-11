using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Represents one runtime-owned observation of a GameView recording.</summary>
public abstract record IpcGameViewRecordingSnapshot
{
    protected IpcGameViewRecordingSnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingRuntimeIdentity runtime,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration,
        ExecutionLifecycle requiredLifecycle)
    {
        if (GameViewRecordingExecutionContract.GetLifecycle(state) != requiredLifecycle)
        {
            throw new ArgumentException(
                "Recording snapshot state must belong to its lifecycle branch.",
                nameof(state));
        }
        if (encodedFrameCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encodedFrameCount),
                encodedFrameCount,
                "Encoded frame count must not be negative.");
        }

        RecordingId = ContractArgumentGuard.RequireNonEmptyGuid(recordingId, nameof(recordingId));
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        State = state;
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        EffectiveMaxDurationSeconds = ContractArgumentGuard.RequirePositive(
            effectiveMaxDurationSeconds,
            nameof(effectiveMaxDurationSeconds));
        EncodedFrameCount = encodedFrameCount;
        UpdatedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc));
        StartGeneration = startGeneration ?? throw new ArgumentNullException(nameof(startGeneration));
        ObservedGeneration = observedGeneration ?? throw new ArgumentNullException(nameof(observedGeneration));
    }

    public Guid RecordingId { get; }

    public Sha256Digest RequestDigest { get; }

    public GameViewRecordingState State { get; }

    public GameViewRecordingRuntimeIdentity Runtime { get; }

    public int EffectiveMaxDurationSeconds { get; }

    public int? EncodedFrameCount { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public UnityEditorGenerationSnapshot StartGeneration { get; }

    public UnityEditorGenerationSnapshot ObservedGeneration { get; }

    [JsonIgnore]
    internal ExecutionLifecycle Lifecycle =>
        GameViewRecordingExecutionContract.GetLifecycle(State);

    [JsonIgnore]
    internal bool IsTerminal => Lifecycle == ExecutionLifecycle.Terminal;

    [JsonIgnore]
    internal abstract GameViewRecordingStopReason? ObservedStopReason { get; }

    [JsonIgnore]
    internal abstract IpcError? ObservedFailure { get; }

    [JsonIgnore]
    internal abstract GameViewRecordingCleanupRecord? ObservedCleanup { get; }

    [JsonIgnore]
    internal abstract GameViewRecordingTargetObservation? ObservedTarget { get; }

    [JsonIgnore]
    internal abstract GameViewRecordingTimingObservation? ObservedTiming { get; }

    [JsonIgnore]
    internal abstract DateTimeOffset? ObservedStartedAtUtc { get; }

    [JsonIgnore]
    internal abstract DateTimeOffset? ObservedStopRequestedAtUtc { get; }

    [JsonIgnore]
    internal abstract DateTimeOffset? ObservedCompletedAtUtc { get; }

    internal bool TryGetTerminal (
        [NotNullWhen(true)]
        out IpcGameViewRecordingTerminalSnapshot? terminalSnapshot)
    {
        terminalSnapshot = this as IpcGameViewRecordingTerminalSnapshot;
        return terminalSnapshot is not null;
    }

    internal IpcGameViewRecordingStopSnapshot RequireStopSnapshot () =>
        this as IpcGameViewRecordingStopSnapshot
        ?? throw new InvalidOperationException(
            "A recording stop snapshot must be in recovery or terminal lifecycle.");

    /// <summary>
    /// Creates the lifecycle-specific snapshot represented by one adapter observation.
    /// </summary>
    internal static IpcGameViewRecordingSnapshot Create (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason? stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
    {
        return state switch
        {
            GameViewRecordingState.Preparing or GameViewRecordingState.Recording =>
                CreateActive(
                    recordingId,
                    requestDigest,
                    state,
                    stopReason,
                    failure,
                    runtime,
                    cleanup,
                    target,
                    timing,
                    effectiveMaxDurationSeconds,
                    encodedFrameCount,
                    startedAtUtc,
                    stopRequestedAtUtc,
                    completedAtUtc,
                    updatedAtUtc,
                    startGeneration,
                    observedGeneration),
            GameViewRecordingState.Finalizing =>
                CreateRecovery(
                    recordingId,
                    requestDigest,
                    state,
                    stopReason,
                    failure,
                    runtime,
                    cleanup,
                    target,
                    timing,
                    effectiveMaxDurationSeconds,
                    encodedFrameCount,
                    startedAtUtc,
                    stopRequestedAtUtc,
                    completedAtUtc,
                    updatedAtUtc,
                    startGeneration,
                    observedGeneration),
            GameViewRecordingState.Completed =>
                CreateCompleted(
                    recordingId,
                    requestDigest,
                    state,
                    stopReason,
                    failure,
                    runtime,
                    cleanup,
                    target,
                    timing,
                    effectiveMaxDurationSeconds,
                    encodedFrameCount,
                    startedAtUtc,
                    stopRequestedAtUtc,
                    completedAtUtc,
                    updatedAtUtc,
                    startGeneration,
                    observedGeneration),
            GameViewRecordingState.Failed =>
                CreateFailed(
                    recordingId,
                    requestDigest,
                    state,
                    stopReason,
                    failure,
                    runtime,
                    cleanup,
                    target,
                    timing,
                    effectiveMaxDurationSeconds,
                    encodedFrameCount,
                    startedAtUtc,
                    stopRequestedAtUtc,
                    completedAtUtc,
                    updatedAtUtc,
                    startGeneration,
                    observedGeneration),
            GameViewRecordingState.Indeterminate =>
                CreateIndeterminate(
                    recordingId,
                    requestDigest,
                    state,
                    stopReason,
                    failure,
                    runtime,
                    cleanup,
                    target,
                    timing,
                    effectiveMaxDurationSeconds,
                    encodedFrameCount,
                    startedAtUtc,
                    stopRequestedAtUtc,
                    completedAtUtc,
                    updatedAtUtc,
                    startGeneration,
                    observedGeneration),
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Recording snapshot state must be defined."),
        };
    }

    protected static DateTimeOffset? RequireOptionalUtc (
        DateTimeOffset? value,
        string parameterName) =>
        value.HasValue
            ? ContractArgumentGuard.RequireUtcTimestamp(value.Value, parameterName)
            : null;

    protected static void EnsureTimeline (
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if ((startedAtUtc.HasValue && startedAtUtc > updatedAtUtc)
            || (stopRequestedAtUtc.HasValue && stopRequestedAtUtc > updatedAtUtc)
            || (completedAtUtc.HasValue && completedAtUtc > updatedAtUtc))
        {
            throw new ArgumentException(
                "Runtime recording timestamps must not occur after the observation update time.");
        }
    }

    protected static void EnsureTerminalObservationConsistency (
        Guid recordingId,
        Sha256Digest requestDigest,
        int? encodedFrameCount,
        DateTimeOffset completedAtUtc,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTimingObservation? timing)
    {
        if (cleanup is not null
            && (cleanup.RecordingId != recordingId
                || cleanup.RequestDigest != requestDigest
                || cleanup.CompletedAtUtc != completedAtUtc))
        {
            throw new ArgumentException(
                "Runtime cleanup must match the recording snapshot identity and completion time.",
                nameof(cleanup));
        }
        if (timing?.EncodedFrameCount is int observedFrameCount
            && encodedFrameCount is int reportedFrameCount
            && observedFrameCount != reportedFrameCount)
        {
            throw new ArgumentException(
                "Runtime frame-count observations must agree.",
                nameof(encodedFrameCount));
        }
    }

    private static IpcGameViewRecordingActiveSnapshot CreateActive (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason? stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
    {
        if (stopReason.HasValue
            || failure is not null
            || cleanup is not null
            || timing is not null
            || stopRequestedAtUtc.HasValue
            || completedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "An active recording snapshot cannot carry stop or terminal observations.",
                nameof(state));
        }

        return new IpcGameViewRecordingActiveSnapshot(
            recordingId,
            requestDigest,
            state,
            runtime,
            target,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            startedAtUtc,
            updatedAtUtc,
            startGeneration,
            observedGeneration);
    }

    private static IpcGameViewRecordingRecoverySnapshot CreateRecovery (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason? stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
    {
        if (!stopReason.HasValue)
        {
            throw new ArgumentException(
                "A recovery recording snapshot requires a stop reason.",
                nameof(stopReason));
        }
        if (cleanup is not null || timing is not null || completedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "A recovery recording snapshot cannot carry terminal observations.",
                nameof(cleanup));
        }

        return new IpcGameViewRecordingRecoverySnapshot(
            recordingId,
            requestDigest,
            state,
            stopReason.Value,
            failure,
            runtime,
            target,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            startedAtUtc,
            stopRequestedAtUtc,
            updatedAtUtc,
            startGeneration,
            observedGeneration);
    }

    private static IpcGameViewRecordingCompletedSnapshot CreateCompleted (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason? stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
    {
        if (failure is not null)
        {
            throw new ArgumentException(
                "A completed recording snapshot cannot carry a failure.",
                nameof(failure));
        }

        return new IpcGameViewRecordingCompletedSnapshot(
            recordingId,
            requestDigest,
            state,
            RequireValue(stopReason, nameof(stopReason)),
            runtime,
            RequireReference(cleanup, nameof(cleanup)),
            RequireReference(target, nameof(target)),
            RequireReference(timing, nameof(timing)),
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            RequireValue(startedAtUtc, nameof(startedAtUtc)),
            stopRequestedAtUtc,
            RequireValue(completedAtUtc, nameof(completedAtUtc)),
            updatedAtUtc,
            startGeneration,
            observedGeneration);
    }

    private static IpcGameViewRecordingFailedSnapshot CreateFailed (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason? stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration) =>
        new(
            recordingId,
            requestDigest,
            state,
            RequireValue(stopReason, nameof(stopReason)),
            RequireReference(failure, nameof(failure)),
            runtime,
            RequireReference(cleanup, nameof(cleanup)),
            RequireReference(target, nameof(target)),
            RequireReference(timing, nameof(timing)),
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            RequireValue(startedAtUtc, nameof(startedAtUtc)),
            stopRequestedAtUtc,
            RequireValue(completedAtUtc, nameof(completedAtUtc)),
            updatedAtUtc,
            startGeneration,
            observedGeneration);

    private static IpcGameViewRecordingIndeterminateSnapshot CreateIndeterminate (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason? stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration) =>
        new(
            recordingId,
            requestDigest,
            state,
            RequireValue(stopReason, nameof(stopReason)),
            failure,
            runtime,
            cleanup,
            target,
            timing,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            startedAtUtc,
            stopRequestedAtUtc,
            RequireValue(completedAtUtc, nameof(completedAtUtc)),
            updatedAtUtc,
            startGeneration,
            observedGeneration);

    private static T RequireReference<T> (T? value, string parameterName)
        where T : class =>
        value ?? throw new ArgumentException("A required recording observation is missing.", parameterName);

    private static T RequireValue<T> (T? value, string parameterName)
        where T : struct =>
        value ?? throw new ArgumentException("A required recording observation is missing.", parameterName);
}

/// <summary>Represents a recording that remains in normal forward progress.</summary>
public sealed record IpcGameViewRecordingActiveSnapshot : IpcGameViewRecordingSnapshot
{
    [JsonConstructor]
    public IpcGameViewRecordingActiveSnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingTargetObservation? target,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
        : base(
            recordingId,
            requestDigest,
            state,
            runtime,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            updatedAtUtc,
            startGeneration,
            observedGeneration,
            ExecutionLifecycle.Active)
    {
        Target = target;
        StartedAtUtc = RequireOptionalUtc(startedAtUtc, nameof(startedAtUtc));
        EnsureTimeline(StartedAtUtc, stopRequestedAtUtc: null, completedAtUtc: null, UpdatedAtUtc);
    }

    public GameViewRecordingTargetObservation? Target { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    internal override GameViewRecordingStopReason? ObservedStopReason => null;

    internal override IpcError? ObservedFailure => null;

    internal override GameViewRecordingCleanupRecord? ObservedCleanup => null;

    internal override GameViewRecordingTargetObservation? ObservedTarget => Target;

    internal override GameViewRecordingTimingObservation? ObservedTiming => null;

    internal override DateTimeOffset? ObservedStartedAtUtc => StartedAtUtc;

    internal override DateTimeOffset? ObservedStopRequestedAtUtc => null;

    internal override DateTimeOffset? ObservedCompletedAtUtc => null;
}

/// <summary>Represents a recording being finalized or recovered.</summary>
public sealed record IpcGameViewRecordingRecoverySnapshot : IpcGameViewRecordingStopSnapshot
{
    [JsonConstructor]
    public IpcGameViewRecordingRecoverySnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingTargetObservation? target,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
        : base(
            recordingId,
            requestDigest,
            state,
            runtime,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            updatedAtUtc,
            startGeneration,
            observedGeneration,
            ExecutionLifecycle.Recovery)
    {
        EnsureDefinedStopReason(stopReason, nameof(stopReason));
        StopReason = stopReason;
        Failure = failure;
        Target = target;
        StartedAtUtc = RequireOptionalUtc(startedAtUtc, nameof(startedAtUtc));
        StopRequestedAtUtc = RequireOptionalUtc(stopRequestedAtUtc, nameof(stopRequestedAtUtc));
        EnsureTimeline(StartedAtUtc, StopRequestedAtUtc, completedAtUtc: null, UpdatedAtUtc);
    }

    public GameViewRecordingStopReason StopReason { get; }

    public IpcError? Failure { get; }

    public GameViewRecordingTargetObservation? Target { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    public DateTimeOffset? StopRequestedAtUtc { get; }

    internal override GameViewRecordingStopReason? ObservedStopReason => StopReason;

    internal override IpcError? ObservedFailure => Failure;

    internal override GameViewRecordingCleanupRecord? ObservedCleanup => null;

    internal override GameViewRecordingTargetObservation? ObservedTarget => Target;

    internal override GameViewRecordingTimingObservation? ObservedTiming => null;

    internal override DateTimeOffset? ObservedStartedAtUtc => StartedAtUtc;

    internal override DateTimeOffset? ObservedStopRequestedAtUtc => StopRequestedAtUtc;

    internal override DateTimeOffset? ObservedCompletedAtUtc => null;
}

/// <summary>Represents a snapshot that can be returned by a successful stop request.</summary>
public abstract record IpcGameViewRecordingStopSnapshot : IpcGameViewRecordingSnapshot
{
    protected IpcGameViewRecordingStopSnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingRuntimeIdentity runtime,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration,
        ExecutionLifecycle requiredLifecycle)
        : base(
            recordingId,
            requestDigest,
            state,
            runtime,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            updatedAtUtc,
            startGeneration,
            observedGeneration,
            requiredLifecycle)
    {
        if (requiredLifecycle == ExecutionLifecycle.Active)
        {
            throw new ArgumentException(
                "A stop snapshot must be in recovery or terminal lifecycle.",
                nameof(requiredLifecycle));
        }
    }

    protected static void EnsureDefinedStopReason (
        GameViewRecordingStopReason stopReason,
        string parameterName)
    {
        if (!TextVocabulary.IsDefined(stopReason))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                stopReason,
                "Recording stop reason must be defined.");
        }
    }
}

/// <summary>Represents a recording with a completed runtime interval.</summary>
public abstract record IpcGameViewRecordingTerminalSnapshot : IpcGameViewRecordingStopSnapshot
{
    protected IpcGameViewRecordingTerminalSnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason stopReason,
        GameViewRecordingRuntimeIdentity runtime,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
        : base(
            recordingId,
            requestDigest,
            state,
            runtime,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            updatedAtUtc,
            startGeneration,
            observedGeneration,
            ExecutionLifecycle.Terminal)
    {
        EnsureDefinedStopReason(stopReason, nameof(stopReason));
        StopReason = stopReason;
        CompletedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            completedAtUtc,
            nameof(completedAtUtc));
        EnsureTimeline(
            startedAtUtc: null,
            stopRequestedAtUtc: null,
            CompletedAtUtc,
            UpdatedAtUtc);
    }

    public GameViewRecordingStopReason StopReason { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    internal override GameViewRecordingStopReason? ObservedStopReason => StopReason;

    internal override DateTimeOffset? ObservedCompletedAtUtc => CompletedAtUtc;

    internal bool TryGetCompleted (
        [NotNullWhen(true)]
        out IpcGameViewRecordingCompletedSnapshot? completedSnapshot)
    {
        completedSnapshot = this as IpcGameViewRecordingCompletedSnapshot;
        return completedSnapshot is not null;
    }

    internal bool TryGetFailed (
        [NotNullWhen(true)]
        out IpcGameViewRecordingFailedSnapshot? failedSnapshot)
    {
        failedSnapshot = this as IpcGameViewRecordingFailedSnapshot;
        return failedSnapshot is not null;
    }
}

/// <summary>Represents a successful runtime recording with complete terminal observations.</summary>
public sealed record IpcGameViewRecordingCompletedSnapshot : IpcGameViewRecordingTerminalSnapshot
{
    [JsonConstructor]
    public IpcGameViewRecordingCompletedSnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason stopReason,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord cleanup,
        GameViewRecordingTargetObservation target,
        GameViewRecordingTimingObservation timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
        : base(
            recordingId,
            requestDigest,
            state,
            stopReason,
            runtime,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            completedAtUtc,
            updatedAtUtc,
            startGeneration,
            observedGeneration)
    {
        if (state != GameViewRecordingState.Completed)
        {
            throw new ArgumentException(
                "A completed recording snapshot requires the completed state.",
                nameof(state));
        }

        Cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        StartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(startedAtUtc, nameof(startedAtUtc));
        StopRequestedAtUtc = RequireOptionalUtc(stopRequestedAtUtc, nameof(stopRequestedAtUtc));
        EnsureTimeline(StartedAtUtc, StopRequestedAtUtc, CompletedAtUtc, UpdatedAtUtc);
        EnsureTerminalObservationConsistency(
            RecordingId,
            RequestDigest,
            EncodedFrameCount,
            CompletedAtUtc,
            Cleanup,
            Timing);
        if (!IsReadyForApplicationFinalization(Cleanup))
        {
            throw new ArgumentException(
                "A completed runtime recording must leave only the application-owned temporary output pending release.",
                nameof(cleanup));
        }
    }

    public GameViewRecordingCleanupRecord Cleanup { get; }

    public GameViewRecordingTargetObservation Target { get; }

    public GameViewRecordingTimingObservation Timing { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? StopRequestedAtUtc { get; }

    internal override IpcError? ObservedFailure => null;

    internal override GameViewRecordingCleanupRecord? ObservedCleanup => Cleanup;

    internal override GameViewRecordingTargetObservation? ObservedTarget => Target;

    internal override GameViewRecordingTimingObservation? ObservedTiming => Timing;

    internal override DateTimeOffset? ObservedStartedAtUtc => StartedAtUtc;

    internal override DateTimeOffset? ObservedStopRequestedAtUtc => StopRequestedAtUtc;

    private static bool IsReadyForApplicationFinalization (GameViewRecordingCleanupRecord cleanup)
    {
        if (cleanup.Disposition != GameViewRecordingCleanupDisposition.Unconfirmed
            || cleanup.StateRestorations.Any(static restoration =>
                restoration.Disposition is GameViewRecordingStateRestorationDisposition.Failed
                    or GameViewRecordingStateRestorationDisposition.Unconfirmed))
        {
            return false;
        }

        foreach (var release in cleanup.ResourceReleases)
        {
            if (release.Kind == GameViewRecordingResourceKind.TemporaryOutput)
            {
                if (!release.Acquired
                    || release.ReleaseAttempted
                    || release.Disposition != GameViewRecordingResourceReleaseDisposition.Unconfirmed
                    || release.ReasonCode is not null)
                {
                    return false;
                }

                continue;
            }

            if (release.Disposition is GameViewRecordingResourceReleaseDisposition.Failed
                or GameViewRecordingResourceReleaseDisposition.Unconfirmed)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Represents a failed runtime recording with complete terminal observations.</summary>
public sealed record IpcGameViewRecordingFailedSnapshot : IpcGameViewRecordingTerminalSnapshot
{
    [JsonConstructor]
    public IpcGameViewRecordingFailedSnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason stopReason,
        IpcError failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord cleanup,
        GameViewRecordingTargetObservation target,
        GameViewRecordingTimingObservation timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
        : base(
            recordingId,
            requestDigest,
            state,
            stopReason,
            runtime,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            completedAtUtc,
            updatedAtUtc,
            startGeneration,
            observedGeneration)
    {
        if (state != GameViewRecordingState.Failed)
        {
            throw new ArgumentException(
                "A failed recording snapshot requires the failed state.",
                nameof(state));
        }

        Failure = failure ?? throw new ArgumentNullException(nameof(failure));
        Cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        StartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(startedAtUtc, nameof(startedAtUtc));
        StopRequestedAtUtc = RequireOptionalUtc(stopRequestedAtUtc, nameof(stopRequestedAtUtc));
        EnsureTimeline(StartedAtUtc, StopRequestedAtUtc, CompletedAtUtc, UpdatedAtUtc);
        EnsureTerminalObservationConsistency(
            RecordingId,
            RequestDigest,
            EncodedFrameCount,
            CompletedAtUtc,
            Cleanup,
            Timing);
    }

    public IpcError Failure { get; }

    public GameViewRecordingCleanupRecord Cleanup { get; }

    public GameViewRecordingTargetObservation Target { get; }

    public GameViewRecordingTimingObservation Timing { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? StopRequestedAtUtc { get; }

    internal override IpcError? ObservedFailure => Failure;

    internal override GameViewRecordingCleanupRecord? ObservedCleanup => Cleanup;

    internal override GameViewRecordingTargetObservation? ObservedTarget => Target;

    internal override GameViewRecordingTimingObservation? ObservedTiming => Timing;

    internal override DateTimeOffset? ObservedStartedAtUtc => StartedAtUtc;

    internal override DateTimeOffset? ObservedStopRequestedAtUtc => StopRequestedAtUtc;
}

/// <summary>Represents a terminal runtime recording whose complete terminal facts are unavailable.</summary>
public sealed record IpcGameViewRecordingIndeterminateSnapshot : IpcGameViewRecordingTerminalSnapshot
{
    [JsonConstructor]
    public IpcGameViewRecordingIndeterminateSnapshot (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingState state,
        GameViewRecordingStopReason stopReason,
        IpcError? failure,
        GameViewRecordingRuntimeIdentity runtime,
        GameViewRecordingCleanupRecord? cleanup,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        int effectiveMaxDurationSeconds,
        int? encodedFrameCount,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? stopRequestedAtUtc,
        DateTimeOffset completedAtUtc,
        DateTimeOffset updatedAtUtc,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot observedGeneration)
        : base(
            recordingId,
            requestDigest,
            state,
            stopReason,
            runtime,
            effectiveMaxDurationSeconds,
            encodedFrameCount,
            completedAtUtc,
            updatedAtUtc,
            startGeneration,
            observedGeneration)
    {
        if (state != GameViewRecordingState.Indeterminate)
        {
            throw new ArgumentException(
                "An indeterminate recording snapshot requires the indeterminate state.",
                nameof(state));
        }

        Failure = failure;
        Cleanup = cleanup;
        Target = target;
        Timing = timing;
        StartedAtUtc = RequireOptionalUtc(startedAtUtc, nameof(startedAtUtc));
        StopRequestedAtUtc = RequireOptionalUtc(stopRequestedAtUtc, nameof(stopRequestedAtUtc));
        EnsureTimeline(StartedAtUtc, StopRequestedAtUtc, CompletedAtUtc, UpdatedAtUtc);
        EnsureTerminalObservationConsistency(
            RecordingId,
            RequestDigest,
            EncodedFrameCount,
            CompletedAtUtc,
            Cleanup,
            Timing);
    }

    public IpcError? Failure { get; }

    public GameViewRecordingCleanupRecord? Cleanup { get; }

    public GameViewRecordingTargetObservation? Target { get; }

    public GameViewRecordingTimingObservation? Timing { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    public DateTimeOffset? StopRequestedAtUtc { get; }

    internal override IpcError? ObservedFailure => Failure;

    internal override GameViewRecordingCleanupRecord? ObservedCleanup => Cleanup;

    internal override GameViewRecordingTargetObservation? ObservedTarget => Target;

    internal override GameViewRecordingTimingObservation? ObservedTiming => Timing;

    internal override DateTimeOffset? ObservedStartedAtUtc => StartedAtUtc;

    internal override DateTimeOffset? ObservedStopRequestedAtUtc => StopRequestedAtUtc;
}

/// <summary>Represents a runtime recording selection returned over IPC.</summary>
public abstract record IpcGameViewRecordingSelection;

/// <summary>Indicates that the runtime registry contains no selected recording.</summary>
public sealed record IpcNoGameViewRecordingSelection : IpcGameViewRecordingSelection;

/// <summary>Contains the runtime recording selected by identifier or current-environment lookup.</summary>
public sealed record IpcSelectedGameViewRecordingSelection : IpcGameViewRecordingSelection
{
    [JsonConstructor]
    public IpcSelectedGameViewRecordingSelection (IpcGameViewRecordingSnapshot recording)
    {
        Recording = recording ?? throw new ArgumentNullException(nameof(recording));
    }

    public IpcGameViewRecordingSnapshot Recording { get; }
}
