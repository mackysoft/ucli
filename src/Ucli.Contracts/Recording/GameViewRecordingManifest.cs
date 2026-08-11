using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Identifies the Unity runtime and encoder that owned one recording.</summary>
public sealed record GameViewRecordingRuntimeIdentity
{
    [JsonConstructor]
    public GameViewRecordingRuntimeIdentity (
        Guid runtimeId,
        string operatingSystem,
        string encoderName,
        string encoderVersion)
    {
        RuntimeId = ContractArgumentGuard.RequireNonEmptyGuid(runtimeId, nameof(runtimeId));
        OperatingSystem = ContractArgumentGuard.RequireValue(operatingSystem, nameof(operatingSystem));
        EncoderName = ContractArgumentGuard.RequireValue(encoderName, nameof(encoderName));
        EncoderVersion = ContractArgumentGuard.RequireValue(encoderVersion, nameof(encoderVersion));
    }

    public Guid RuntimeId { get; }

    public string OperatingSystem { get; }

    public string EncoderName { get; }

    public string EncoderVersion { get; }
}

/// <summary>Identifies the Recorder package, adapter, and effective encoder profile.</summary>
public sealed record GameViewRecordingProviderIdentity
{
    [JsonConstructor]
    public GameViewRecordingProviderIdentity (
        string recorderPackageId,
        string recorderPackageVersion,
        string adapterId,
        string adapterVersion,
        GameViewRecordingCaptureProfile captureProfile)
    {
        if (!string.Equals(recorderPackageId, GameViewRecorderCompatibilityMetadata.PackageId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Recorder package id must match the bundled compatibility metadata.", nameof(recorderPackageId));
        }
        if (!string.Equals(adapterId, GameViewRecorderCompatibilityMetadata.AdapterId, StringComparison.Ordinal)
            || !string.Equals(adapterVersion, GameViewRecorderCompatibilityMetadata.AdapterVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Adapter identity must match the bundled compatibility metadata.", nameof(adapterId));
        }

        RecorderPackageId = recorderPackageId;
        RecorderPackageVersion = ContractArgumentGuard.RequireValue(recorderPackageVersion, nameof(recorderPackageVersion));
        AdapterId = adapterId;
        AdapterVersion = adapterVersion;
        CaptureProfile = captureProfile ?? throw new ArgumentNullException(nameof(captureProfile));
    }

    public string RecorderPackageId { get; }

    public string RecorderPackageVersion { get; }

    public string AdapterId { get; }

    public string AdapterVersion { get; }

    public GameViewRecordingCaptureProfile CaptureProfile { get; }
}

/// <summary>Records the fixed GameView surface and observed output dimensions.</summary>
public sealed record GameViewRecordingTargetObservation
{
    [JsonConstructor]
    public GameViewRecordingTargetObservation (
        string playModeViewId,
        string gameViewId,
        int display,
        PixelDimensions requestedDimensions,
        PixelDimensions dimensions,
        string orientation,
        UnityProjectColorSpace projectColorSpace)
    {
        PlayModeViewId = ContractArgumentGuard.RequireValue(playModeViewId, nameof(playModeViewId));
        GameViewId = ContractArgumentGuard.RequireValue(gameViewId, nameof(gameViewId));
        Display = ContractArgumentGuard.RequireNonNegative(display, nameof(display));
        RequestedDimensions = requestedDimensions ?? throw new ArgumentNullException(nameof(requestedDimensions));
        Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
        Orientation = ContractArgumentGuard.RequireValue(orientation, nameof(orientation));
        if (!TextVocabulary.IsDefined(projectColorSpace))
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectColorSpace),
                projectColorSpace,
                "Unity project color space must be specified.");
        }

        ProjectColorSpace = projectColorSpace;
    }

    public string PlayModeViewId { get; }

    public string GameViewId { get; }

    public int Display { get; }

    public PixelDimensions RequestedDimensions { get; }

    public PixelDimensions Dimensions { get; }

    public string Orientation { get; }

    public UnityProjectColorSpace ProjectColorSpace { get; }
}

/// <summary>Records independent timing and frame observations for one recording interval.</summary>
public sealed record GameViewRecordingTimingObservation
{
    [JsonConstructor]
    public GameViewRecordingTimingObservation (
        long? monotonicStartedTimestamp,
        long? monotonicStopRequestedTimestamp,
        long monotonicCompletedTimestamp,
        long monotonicFrequency,
        double? gameTimeStartedSeconds,
        double? gameTimeCompletedSeconds,
        double? timeScaleStarted,
        double? timeScaleCompleted,
        int? frameCountStarted,
        int? frameCountCompleted,
        double? mp4DurationSeconds,
        int? encodedFrameCount,
        double? effectiveFrameRate,
        int? droppedFrameCount,
        int? duplicatedFrameCount,
        int? delayedFrameCount)
    {
        if (monotonicFrequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monotonicFrequency), monotonicFrequency, "Monotonic clock frequency must be positive.");
        }
        if ((monotonicStartedTimestamp.HasValue && monotonicStartedTimestamp > monotonicCompletedTimestamp)
            || (monotonicStopRequestedTimestamp.HasValue && monotonicStopRequestedTimestamp > monotonicCompletedTimestamp))
        {
            throw new ArgumentException("Monotonic recording timestamps must not occur after completion.");
        }

        MonotonicStartedTimestamp = monotonicStartedTimestamp;
        MonotonicStopRequestedTimestamp = monotonicStopRequestedTimestamp;
        MonotonicCompletedTimestamp = monotonicCompletedTimestamp;
        MonotonicFrequency = monotonicFrequency;
        GameTimeStartedSeconds = RequireOptionalFinite(gameTimeStartedSeconds, nameof(gameTimeStartedSeconds));
        GameTimeCompletedSeconds = RequireOptionalFinite(gameTimeCompletedSeconds, nameof(gameTimeCompletedSeconds));
        TimeScaleStarted = RequireOptionalFinite(timeScaleStarted, nameof(timeScaleStarted));
        TimeScaleCompleted = RequireOptionalFinite(timeScaleCompleted, nameof(timeScaleCompleted));
        FrameCountStarted = RequireOptionalNonNegative(frameCountStarted, nameof(frameCountStarted));
        FrameCountCompleted = RequireOptionalNonNegative(frameCountCompleted, nameof(frameCountCompleted));
        Mp4DurationSeconds = RequireOptionalNonNegativeFinite(mp4DurationSeconds, nameof(mp4DurationSeconds));
        EncodedFrameCount = RequireOptionalNonNegative(encodedFrameCount, nameof(encodedFrameCount));
        EffectiveFrameRate = RequireOptionalPositiveFinite(effectiveFrameRate, nameof(effectiveFrameRate));
        DroppedFrameCount = RequireOptionalNonNegative(droppedFrameCount, nameof(droppedFrameCount));
        DuplicatedFrameCount = RequireOptionalNonNegative(duplicatedFrameCount, nameof(duplicatedFrameCount));
        DelayedFrameCount = RequireOptionalNonNegative(delayedFrameCount, nameof(delayedFrameCount));
    }

    public long? MonotonicStartedTimestamp { get; }

    public long? MonotonicStopRequestedTimestamp { get; }

    public long MonotonicCompletedTimestamp { get; }

    public long MonotonicFrequency { get; }

    public double? GameTimeStartedSeconds { get; }

    public double? GameTimeCompletedSeconds { get; }

    public double? TimeScaleStarted { get; }

    public double? TimeScaleCompleted { get; }

    public int? FrameCountStarted { get; }

    public int? FrameCountCompleted { get; }

    public double? Mp4DurationSeconds { get; }

    public int? EncodedFrameCount { get; }

    public double? EffectiveFrameRate { get; }

    public int? DroppedFrameCount { get; }

    public int? DuplicatedFrameCount { get; }

    public int? DelayedFrameCount { get; }

    private static double? RequireOptionalFinite (double? value, string parameterName)
    {
        if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Timing observation must be finite.");
        }
        return value;
    }

    private static double? RequireOptionalNonNegativeFinite (double? value, string parameterName)
    {
        value = RequireOptionalFinite(value, parameterName);
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Timing observation must not be negative.");
        }
        return value;
    }

    private static double? RequireOptionalPositiveFinite (double? value, string parameterName)
    {
        value = RequireOptionalFinite(value, parameterName);
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Timing observation must be positive.");
        }
        return value;
    }

    private static int? RequireOptionalNonNegative (int? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Frame observation must not be negative.");
        }
        return value;
    }
}

/// <summary>Represents the immutable manifest describing one finalized recording.</summary>
public sealed record GameViewRecordingManifest
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public GameViewRecordingManifest (
        int schemaVersion,
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingRequest request,
        UnityProjectIdentity project,
        GameViewRecordingRuntimeIdentity runtime,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot terminalGeneration,
        GameViewRecordingProviderIdentity provider,
        GameViewRecordingTargetObservation? target,
        GameViewRecordingTimingObservation? timing,
        GameViewRecordingTerminalSummary terminalSummary,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Recording manifest schema version must be one.");
        }

        SchemaVersion = schemaVersion;
        RecordingId = ContractArgumentGuard.RequireNonEmptyGuid(recordingId, nameof(recordingId));
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        StartGeneration = startGeneration ?? throw new ArgumentNullException(nameof(startGeneration));
        TerminalGeneration = terminalGeneration ?? throw new ArgumentNullException(nameof(terminalGeneration));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Target = target;
        Timing = timing;
        TerminalSummary = terminalSummary ?? throw new ArgumentNullException(nameof(terminalSummary));
        ArtifactRefs = ContractArgumentGuard.RequireItems(artifactRefs, nameof(artifactRefs));
        Diagnostics = ContractArgumentGuard.RequireItems(diagnostics, nameof(diagnostics));

        if (terminalSummary.State is GameViewRecordingState.Completed or GameViewRecordingState.Failed
            && (target is null || timing is null))
        {
            throw new ArgumentException(
                "A completed or failed manifest requires target and timing observations.",
                nameof(target));
        }
        if (target is not null && target.RequestedDimensions != request.Resolution)
        {
            throw new ArgumentException("Manifest target and normalized request must use the same requested dimensions.", nameof(target));
        }
    }

    public int SchemaVersion { get; }

    public Guid RecordingId { get; }

    public Sha256Digest RequestDigest { get; }

    public GameViewRecordingRequest Request { get; }

    public UnityProjectIdentity Project { get; }

    public GameViewRecordingRuntimeIdentity Runtime { get; }

    public UnityEditorGenerationSnapshot StartGeneration { get; }

    public UnityEditorGenerationSnapshot TerminalGeneration { get; }

    public GameViewRecordingProviderIdentity Provider { get; }

    public GameViewRecordingTargetObservation? Target { get; }

    public GameViewRecordingTimingObservation? Timing { get; }

    public GameViewRecordingTerminalSummary TerminalSummary { get; }

    public IReadOnlyList<ArtifactRef> ArtifactRefs { get; }

    public IReadOnlyList<GameViewRecordingDiagnostic> Diagnostics { get; }
}
