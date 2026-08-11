using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Tests.Recording;

internal static class GameViewRecordingContractTestFactory
{
    public static readonly Guid RecordingId = Guid.Parse("2b5b01d2-00ed-40a1-b956-69de49a47629");

    public static readonly Guid RuntimeId = Guid.Parse("07a6ae8e-2e6f-4437-b04b-38bda65a8f61");

    public static readonly DateTimeOffset StartedAtUtc = new(2026, 8, 5, 1, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset CompletedAtUtc = new(2026, 8, 5, 1, 0, 2, TimeSpan.Zero);

    public static readonly DateTimeOffset DispatchDeadlineUtc = new(2026, 8, 5, 1, 0, 10, TimeSpan.Zero);

    public static Sha256Digest RequestDigest { get; } = Sha256Digest.Compute([1, 2, 3, 4]);

    public static UnityProjectIdentity CreateProject () =>
        new("C:/repo", new ProjectFingerprint(new string('a', 64)), "6000.0.0f1");

    public static GameViewRecordingRequest CreateEffectiveRequest () =>
        new(
            GameViewRecordingRequest.CurrentSchemaVersion,
            new PixelDimensions(1920, 1080),
            frameRate: 30,
            maxDurationSeconds: 120);

    public static GameViewRecordingCaptureProfile CreateCaptureProfile () =>
        new(
            GameViewRecordingContainer.Mp4,
            GameViewRecordingCodec.H264,
            audio: false,
            alpha: false,
            encodingProfile: "h264-main",
            encodingQuality: "high",
            GameViewRecordingTimingMode.ConstantFrameRateCapture);

    public static ArtifactRef CreateRequestRef () =>
        CreateArtifactRef(
            GameViewRecordingArtifactKinds.Request,
            GameViewRecordingArtifactMediaTypes.Json,
            "recordings/request.json",
            RequestDigest);

    public static ArtifactRef CreateTerminalRecordRef () =>
        CreateArtifactRef(
            GameViewRecordingArtifactKinds.TerminalRecord,
            GameViewRecordingArtifactMediaTypes.Json,
            "recordings/terminal.json",
            Sha256Digest.Compute([9, 8, 7]));

    public static ArtifactRef CreateArtifactRef (
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        string path,
        Sha256Digest? digest = null)
    {
        return new PathArtifactRef(
            kind,
            mediaType,
            new ArtifactPath(path),
            digest ?? Sha256Digest.Compute([5, 6, 7]),
            sizeBytes: 10,
            StartedAtUtc);
    }

    public static GameViewRecordingActivePayload CreateActivePayload ()
    {
        var requestRef = CreateRequestRef();
        var progress = new GameViewRecordingActiveProgress(
            GameViewRecordingState.Recording,
            effectiveMaxDurationSeconds: 120,
            encodedFrameCount: 10,
            startedAtUtc: StartedAtUtc,
            stopRequestedAtUtc: null,
            updatedAtUtc: StartedAtUtc.AddSeconds(1));
        return new GameViewRecordingActivePayload(
            CreateProject(),
            new ActiveExecutionRef(
                GameViewRecordingExecutionContract.Kind,
                RecordingId,
                RequestDigest,
                GameViewRecordingExecutionContract.ToExecutionState(progress.State),
                new ExecutionStatusLocator("recordings/active")),
            RequestDigest,
            requestRef,
            progress,
            [requestRef],
            Array.Empty<GameViewRecordingDiagnostic>());
    }

    public static GameViewRecordingRecoveryPayload CreateRecoveryPayload ()
    {
        var requestRef = CreateRequestRef();
        var progress = new GameViewRecordingRecoveryProgress(
            GameViewRecordingState.Finalizing,
            effectiveMaxDurationSeconds: 120,
            encodedFrameCount: 60,
            startedAtUtc: StartedAtUtc,
            stopRequestedAtUtc: StartedAtUtc.AddSeconds(1),
            updatedAtUtc: CompletedAtUtc);
        return new GameViewRecordingRecoveryPayload(
            CreateProject(),
            new RecoveryExecutionRef(
                GameViewRecordingExecutionContract.Kind,
                RecordingId,
                RequestDigest,
                GameViewRecordingExecutionContract.ToExecutionState(progress.State),
                new ExecutionStatusLocator("recordings/recovery")),
            RequestDigest,
            requestRef,
            progress,
            [requestRef],
            Array.Empty<GameViewRecordingDiagnostic>());
    }

    public static GameViewRecordingTerminalPayload CreateTerminalPayload ()
    {
        var requestRef = CreateRequestRef();
        var terminalRecordRef = CreateTerminalRecordRef();
        var progress = new GameViewRecordingTerminalProgress(
            GameViewRecordingState.Completed,
            effectiveMaxDurationSeconds: 120,
            encodedFrameCount: 60,
            startedAtUtc: StartedAtUtc,
            stopRequestedAtUtc: StartedAtUtc.AddSeconds(1),
            updatedAtUtc: CompletedAtUtc);
        return new GameViewRecordingTerminalPayload(
            CreateProject(),
            new TerminalExecutionRef(
                GameViewRecordingExecutionContract.Kind,
                RecordingId,
                RequestDigest,
                GameViewRecordingExecutionContract.ToExecutionState(progress.State),
                statusLocator: null,
                terminalRecordRef),
            RequestDigest,
            requestRef,
            progress,
            [requestRef, terminalRecordRef],
            Array.Empty<GameViewRecordingDiagnostic>(),
            new GameViewRecordingTerminalSummary(
                GameViewRecordingState.Completed,
                GameViewRecordingStopReason.Manual,
                GameViewRecordingVideoDisposition.Available,
                GameViewRecordingCleanupDisposition.Complete,
                StartedAtUtc,
                CompletedAtUtc));
    }

    public static GameViewRecordingCapability CreateMissingCapability () =>
        new(
            new GameViewRecordingPackageCapability(
                GameViewRecordingPackageState.Missing,
                GameViewRecorderCompatibilityMetadata.PackageId,
                version: null),
            new GameViewRecordingCompatibilityCapability(
                GameViewRecordingCompatibilityState.NotApplicable,
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                resolvedVersion: null),
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.NotApplicable,
                adapterId: null,
                adapterVersion: null),
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Unobserved,
                [GameViewRecordingErrorCodes.Unavailable]),
            limits: null,
            captureProfile: null);

    public static UnityEditorGenerationSnapshot CreateGeneration (long generation) =>
        new(generation, generation, generation, generation);

    public static GameViewRecordingRuntimeIdentity CreateRuntime () =>
        new(RuntimeId, "windows", "media-foundation", "1");

    public static IpcGameViewRecordingStartBinding CreateStartBinding () =>
        new(
            new ProcessIdentity(42, 100),
            CreateRuntime(),
            CreateGeneration(1));

    public static IpcGameViewRecordingSnapshot CreateRuntimeSnapshot (GameViewRecordingState state)
    {
        var runtime = CreateRuntime();
        var startGeneration = CreateGeneration(1);
        var observedGeneration = CreateGeneration(2);
        return state switch
        {
            GameViewRecordingState.Preparing or GameViewRecordingState.Recording =>
                new IpcGameViewRecordingActiveSnapshot(
                    RecordingId,
                    RequestDigest,
                    state,
                    runtime,
                    target: null,
                    effectiveMaxDurationSeconds: 120,
                    encodedFrameCount: 60,
                    StartedAtUtc,
                    StartedAtUtc.AddSeconds(1),
                    startGeneration,
                    observedGeneration),
            GameViewRecordingState.Finalizing => new IpcGameViewRecordingRecoverySnapshot(
                RecordingId,
                RequestDigest,
                state,
                GameViewRecordingStopReason.Manual,
                failure: null,
                runtime,
                target: null,
                effectiveMaxDurationSeconds: 120,
                encodedFrameCount: 60,
                StartedAtUtc,
                StartedAtUtc.AddSeconds(1),
                StartedAtUtc.AddSeconds(1),
                startGeneration,
                observedGeneration),
            GameViewRecordingState.Completed => new IpcGameViewRecordingCompletedSnapshot(
                RecordingId,
                RequestDigest,
                state,
                GameViewRecordingStopReason.Manual,
                runtime,
                CreateRuntimeCleanup(),
                CreateRuntimeTarget(),
                CreateRuntimeTiming(),
                effectiveMaxDurationSeconds: 120,
                encodedFrameCount: 60,
                StartedAtUtc,
                StartedAtUtc.AddSeconds(1),
                CompletedAtUtc,
                CompletedAtUtc,
                startGeneration,
                observedGeneration),
            GameViewRecordingState.Failed => new IpcGameViewRecordingFailedSnapshot(
                RecordingId,
                RequestDigest,
                state,
                GameViewRecordingStopReason.Manual,
                new IpcError(
                    GameViewRecordingErrorCodes.FinalizationFailed,
                    "Recording failed.",
                    InstancePath: null),
                runtime,
                CreateRuntimeCleanup(),
                CreateRuntimeTarget(),
                CreateRuntimeTiming(),
                effectiveMaxDurationSeconds: 120,
                encodedFrameCount: 60,
                StartedAtUtc,
                StartedAtUtc.AddSeconds(1),
                CompletedAtUtc,
                CompletedAtUtc,
                startGeneration,
                observedGeneration),
            GameViewRecordingState.Indeterminate => new IpcGameViewRecordingIndeterminateSnapshot(
                RecordingId,
                RequestDigest,
                state,
                GameViewRecordingStopReason.Unconfirmed,
                failure: null,
                runtime,
                cleanup: null,
                target: null,
                timing: null,
                effectiveMaxDurationSeconds: 120,
                encodedFrameCount: 60,
                startedAtUtc: null,
                stopRequestedAtUtc: null,
                CompletedAtUtc,
                CompletedAtUtc,
                startGeneration,
                observedGeneration),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Recording state is not supported."),
        };
    }

    private static GameViewRecordingTargetObservation CreateRuntimeTarget () =>
        new(
            "play-mode-view-1",
            "game-view-1",
            display: 0,
            new PixelDimensions(1920, 1080),
            new PixelDimensions(1920, 1080),
            orientation: "upright",
            projectColorSpace: UnityProjectColorSpace.Linear);

    private static GameViewRecordingTimingObservation CreateRuntimeTiming () =>
        new(
            monotonicStartedTimestamp: 100,
            monotonicStopRequestedTimestamp: 190,
            monotonicCompletedTimestamp: 200,
            monotonicFrequency: 100,
            gameTimeStartedSeconds: 1,
            gameTimeCompletedSeconds: 2,
            timeScaleStarted: 1,
            timeScaleCompleted: 1,
            frameCountStarted: 10,
            frameCountCompleted: 70,
            mp4DurationSeconds: 2,
            encodedFrameCount: 60,
            effectiveFrameRate: 30,
            droppedFrameCount: null,
            duplicatedFrameCount: null,
            delayedFrameCount: null);

    private static GameViewRecordingCleanupRecord CreateRuntimeCleanup ()
    {
        var restorations = Enum.GetValues<GameViewRecordingStateRestorationKind>()
            .Select(static kind => new GameViewRecordingStateRestoration(
                kind,
                beforeValue: "original",
                afterValue: "original",
                changed: false,
                restoreAttempted: false,
                GameViewRecordingStateRestorationDisposition.Unchanged,
                reasonCode: null))
            .ToArray();
        var releases = Enum.GetValues<GameViewRecordingResourceKind>()
            .Select(static kind => kind == GameViewRecordingResourceKind.TemporaryOutput
                ? new GameViewRecordingResourceRelease(
                    kind,
                    acquired: true,
                    releaseAttempted: false,
                    GameViewRecordingResourceReleaseDisposition.Unconfirmed,
                    reasonCode: null)
                : new GameViewRecordingResourceRelease(
                    kind,
                    acquired: false,
                    releaseAttempted: false,
                    GameViewRecordingResourceReleaseDisposition.NotAcquired,
                    reasonCode: null))
            .ToArray();
        return new GameViewRecordingCleanupRecord(
            GameViewRecordingCleanupRecord.CurrentSchemaVersion,
            RecordingId,
            RequestDigest,
            restorations,
            releases,
            GameViewRecordingCleanupDisposition.Unconfirmed,
            CompletedAtUtc);
    }
}
