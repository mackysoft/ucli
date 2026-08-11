using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Tests.Recording;

public sealed class GameViewRecordingArtifactRecordContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CleanupRecord_RoundTripsEveryRequiredOwnedItem ()
    {
        var cleanup = CreateCompleteCleanup();

        var json = JsonSerializer.Serialize(cleanup, IpcJsonSerializerOptions.StrictPropertyNames);
        var roundTripped = JsonSerializer.Deserialize<GameViewRecordingCleanupRecord>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames)!;

        Assert.Equal(GameViewRecordingCleanupDisposition.Complete, roundTripped.Disposition);
        Assert.Equal(6, roundTripped.StateRestorations.Count);
        Assert.Equal(5, roundTripped.ResourceReleases.Count);
        Assert.Equal(
            Enum.GetValues<GameViewRecordingStateRestorationKind>(),
            roundTripped.StateRestorations.Select(static item => item.Kind).OrderBy(static kind => kind));
        Assert.Equal(
            Enum.GetValues<GameViewRecordingResourceKind>(),
            roundTripped.ResourceReleases.Select(static item => item.Kind).OrderBy(static kind => kind));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CleanupRecord_WhenARequiredStateKindIsMissing_RejectsValue ()
    {
        var cleanup = CreateCompleteCleanup();

        Assert.Throws<ArgumentException>(() => new GameViewRecordingCleanupRecord(
            cleanup.SchemaVersion,
            cleanup.RecordingId,
            cleanup.RequestDigest,
            cleanup.StateRestorations.Skip(1).ToArray(),
            cleanup.ResourceReleases,
            cleanup.Disposition,
            cleanup.CompletedAtUtc));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ManifestAndTerminalRecord_RoundTripTheReverificationFacts ()
    {
        var terminalPayload = GameViewRecordingContractTestFactory.CreateTerminalPayload();
        var request = GameViewRecordingContractTestFactory.CreateEffectiveRequest();
        var requestRef = GameViewRecordingContractTestFactory.CreateRequestRef();
        var videoRef = GameViewRecordingContractTestFactory.CreateArtifactRef(
            GameViewRecordingArtifactKinds.Video,
            GameViewRecordingArtifactMediaTypes.Mp4,
            "recordings/game-view.mp4");
        var artifactRefs = new[] { requestRef, videoRef };
        var manifest = new GameViewRecordingManifest(
            GameViewRecordingManifest.CurrentSchemaVersion,
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            request,
            GameViewRecordingContractTestFactory.CreateProject(),
            GameViewRecordingContractTestFactory.CreateRuntime(),
            GameViewRecordingContractTestFactory.CreateGeneration(1),
            GameViewRecordingContractTestFactory.CreateGeneration(2),
            new GameViewRecordingProviderIdentity(
                GameViewRecorderCompatibilityMetadata.PackageId,
                "5.1.5",
                GameViewRecorderCompatibilityMetadata.AdapterId,
                GameViewRecorderCompatibilityMetadata.AdapterVersion,
                GameViewRecordingContractTestFactory.CreateCaptureProfile()),
            new GameViewRecordingTargetObservation(
                "play-mode-view-1",
                "game-view-1",
                display: 0,
                request.Resolution,
                new PixelDimensions(1920, 1080),
                orientation: "topDown",
                projectColorSpace: UnityProjectColorSpace.Linear),
            new GameViewRecordingTimingObservation(
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
                delayedFrameCount: null),
            terminalPayload.TerminalSummary,
            artifactRefs,
            Array.Empty<GameViewRecordingDiagnostic>());
        var terminalRecord = new GameViewRecordingTerminalRecord(
            GameViewRecordingTerminalRecord.CurrentSchemaVersion,
            GameViewRecordingExecutionContract.Kind,
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            GameViewRecordingContractTestFactory.CreateProject(),
            GameViewRecordingContractTestFactory.CreateRuntime(),
            GameViewRecordingContractTestFactory.CreateGeneration(1),
            GameViewRecordingContractTestFactory.CreateGeneration(2),
            terminalPayload.TerminalSummary,
            requestRef,
            artifactRefs,
            Array.Empty<GameViewRecordingDiagnostic>());

        var manifestRoundTrip = RoundTrip(manifest);
        var terminalRoundTrip = RoundTrip(terminalRecord);

        Assert.Equal(GameViewRecordingContractTestFactory.RecordingId, manifestRoundTrip.RecordingId);
        Assert.Equal(30, manifestRoundTrip.Request.FrameRate);
        Assert.Equal("game-view-1", Assert.IsType<GameViewRecordingTargetObservation>(manifestRoundTrip.Target).GameViewId);
        Assert.Equal(60, Assert.IsType<GameViewRecordingTimingObservation>(manifestRoundTrip.Timing).EncodedFrameCount);
        Assert.Equal(GameViewRecordingState.Completed, terminalRoundTrip.TerminalSummary.State);
        Assert.Equal(GameViewRecordingContractTestFactory.RequestDigest, terminalRoundTrip.RequestRef.Digest);
        Assert.Equal(2, terminalRoundTrip.ArtifactRefs.Count);
    }

    private static GameViewRecordingCleanupRecord CreateCompleteCleanup ()
    {
        var stateRestorations = Enum.GetValues<GameViewRecordingStateRestorationKind>()
            .Select(static kind => new GameViewRecordingStateRestoration(
                kind,
                beforeValue: "original",
                afterValue: "original",
                changed: false,
                restoreAttempted: false,
                GameViewRecordingStateRestorationDisposition.Unchanged,
                reasonCode: null))
            .ToArray();
        var resourceReleases = Enum.GetValues<GameViewRecordingResourceKind>()
            .Select(static kind => new GameViewRecordingResourceRelease(
                kind,
                acquired: false,
                releaseAttempted: false,
                GameViewRecordingResourceReleaseDisposition.NotAcquired,
                reasonCode: null))
            .ToArray();
        return new GameViewRecordingCleanupRecord(
            GameViewRecordingCleanupRecord.CurrentSchemaVersion,
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            stateRestorations,
            resourceReleases,
            GameViewRecordingCleanupDisposition.Complete,
            GameViewRecordingContractTestFactory.CompletedAtUtc);
    }

    private static T RoundTrip<T> (T value)
    {
        var json = JsonSerializer.Serialize(value, IpcJsonSerializerOptions.StrictPropertyNames);
        return JsonSerializer.Deserialize<T>(json, IpcJsonSerializerOptions.StrictPropertyNames)!;
    }
}
