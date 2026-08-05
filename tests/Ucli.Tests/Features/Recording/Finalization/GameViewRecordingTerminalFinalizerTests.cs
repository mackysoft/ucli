using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Capability;
using MackySoft.Ucli.Application.Features.Recording.Finalization;
using MackySoft.Ucli.Application.Features.Recording.Projection;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Application.Features.Recording.UseCases;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Features.Recording.Artifacts;
using MackySoft.Ucli.Features.Recording.Artifacts.Mp4;
using MackySoft.Ucli.Features.Recording.Finalization;
using MackySoft.Ucli.Features.Recording.Registry;
using MackySoft.Ucli.Infrastructure.Artifacts;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Features.Recording.Artifacts.Mp4;

namespace MackySoft.Ucli.Tests.Features.Recording.Finalization;

public sealed class GameViewRecordingTerminalFinalizerTests
{
    private static readonly Guid RecordingId =
        Guid.Parse("0f34738a-15c2-4a89-9957-9e035a578c32");
    private static readonly Guid RuntimeId =
        Guid.Parse("fdb7df30-2118-4d95-8ba9-01709454cf14");
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 8, 5, 1, 2, 3, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc =
        StartedAtUtc.AddSeconds(2);
    private static readonly GameViewRecordingRuntimeIdentity Runtime = new(
        RuntimeId,
        "windows",
        "media-foundation",
        "1");
    private static readonly UnityEditorGenerationSnapshot StartGeneration =
        new(1, 2, 3, 4);
    private static readonly IpcGameViewRecordingStartBinding StartBinding = new(
        new ProcessIdentity(ProcessId: 1234, Generation: 5678),
        Runtime,
        StartGeneration);
    private static readonly DateTimeOffset StartDispatchDeadlineUtc =
        StartedAtUtc.AddSeconds(5);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task FinalizeAsync_WithCompletedRuntime_PublishesVerifiedTerminalSetAndDurableCheckpoint ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-finalizer",
            "completed");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(
            scope,
            ProjectFingerprintTestFactory.Create("game-view-recording-finalizer"),
            unityVersion: "6000.3.11f1");
        var context = new ProjectContext(
            project,
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
        var nextPublicationTime = CompletedAtUtc.AddSeconds(1);
        var artifactStore = new FileGameViewRecordingArtifactStore(
            new ImmutableArtifactFilePublisher(() =>
            {
                var current = nextPublicationTime;
                nextPublicationTime = nextPublicationTime.AddSeconds(1);
                return current;
            }),
            new GameViewRecordingMp4Validator());
        var executionStore = new FileGameViewRecordingExecutionStore();
        using var admissionLease = await AcquireAdmissionLeaseAsync(executionStore, project);
        var lease = Assert.IsAssignableFrom<IGameViewRecordingArtifactLease>(
            artifactStore.Prepare(project, RecordingId, admissionLease).Lease);
        var request = CreateEffectiveRequest();
        var requestRef = Assert.IsType<PathArtifactRef>(
            (await lease.PublishRequestAsync(
                request,
                knownArtifact: null,
                CancellationToken.None)).Artifact);
        var terminalSnapshot = CreateCompletedSnapshot(request);
        var stored = CreateTerminalRecoveryStored(
            project,
            request,
            requestRef,
            terminalSnapshot);
        await executionStore.WriteAsync(
            project,
            lease.ExecutionStatePath,
            stored,
            CancellationToken.None);
        await File.WriteAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            SyntheticGameViewRecordingMp4.Create(),
            CancellationToken.None);
        var finalizer = new GameViewRecordingTerminalFinalizer(executionStore);

        var result = await finalizer.FinalizeAsync(
            context,
            lease,
            stored,
            terminalSnapshot,
            CancellationToken.None);

        var success = Assert.IsType<GameViewRecordingTerminalFinalizationSuccess>(result);
        var payload = success.Payload;
        Assert.Equal(GameViewRecordingState.Completed, payload.TerminalSummary.State);
        Assert.Equal(
            [
                GameViewRecordingArtifactKinds.Request,
                GameViewRecordingArtifactKinds.Video,
                GameViewRecordingArtifactKinds.Cleanup,
                GameViewRecordingArtifactKinds.Manifest,
                GameViewRecordingArtifactKinds.TerminalRecord,
            ],
            payload.ArtifactRefs.Select(static artifact => artifact.Kind));
        Assert.True(payload.ArtifactRefs
            .Select(static artifact => artifact.CreatedAtUtc)
            .SequenceEqual(payload.ArtifactRefs
                .Select(static artifact => artifact.CreatedAtUtc)
                .OrderBy(static value => value)));
        Assert.False(File.Exists(ResolveProviderOutputPath(project).Value));

        var durableCheckpoint = await executionStore.ReadAsync(
            project,
            RecordingId,
            CancellationToken.None);
        var durableRecovery = Assert.IsType<GameViewRecordingRecoveryPayload>(
            durableCheckpoint!.Payload);
        Assert.Equal(payload.ArtifactRefs, durableRecovery.ArtifactRefs);

        var cleanup = await ReadArtifactAsync<GameViewRecordingCleanupRecord>(
            project.RepositoryRoot,
            GetArtifact(payload, GameViewRecordingArtifactKinds.Cleanup));
        using var manifest = await ReadArtifactDocumentAsync(
            project.RepositoryRoot,
            GetArtifact(payload, GameViewRecordingArtifactKinds.Manifest));
        using var terminalRecord = await ReadArtifactDocumentAsync(
            project.RepositoryRoot,
            GetArtifact(payload, GameViewRecordingArtifactKinds.TerminalRecord));
        Assert.Equal(GameViewRecordingCleanupDisposition.Complete, cleanup.Disposition);
        Assert.Equal(
            GameViewRecordingResourceReleaseDisposition.Released,
            cleanup.ResourceReleases.Single(static item =>
                item.Kind == GameViewRecordingResourceKind.TemporaryOutput).Disposition);
        var manifestRoot = manifest.RootElement;
        var terminalRoot = terminalRecord.RootElement;
        AssertJsonEquivalent(
            JsonSerializer.SerializeToElement(
                terminalSnapshot.Runtime,
                IpcJsonSerializerOptions.StrictPropertyNames),
            manifestRoot.GetProperty("runtime"));
        AssertJsonEquivalent(
            JsonSerializer.SerializeToElement(
                terminalSnapshot.Target,
                IpcJsonSerializerOptions.StrictPropertyNames),
            manifestRoot.GetProperty("target"));
        Assert.Equal(2, manifestRoot.GetProperty("timing").GetProperty("mp4DurationSeconds").GetDouble());
        Assert.Equal(60, manifestRoot.GetProperty("timing").GetProperty("encodedFrameCount").GetInt32());
        Assert.Equal(30, manifestRoot.GetProperty("timing").GetProperty("effectiveFrameRate").GetDouble());
        AssertJsonEquivalent(
            manifestRoot.GetProperty("terminalSummary"),
            terminalRoot.GetProperty("terminalSummary"));
        AssertJsonEquivalent(
            JsonSerializer.SerializeToElement(
                terminalSnapshot.StartGeneration,
                IpcJsonSerializerOptions.StrictPropertyNames),
            terminalRoot.GetProperty("startGeneration"));
        AssertJsonEquivalent(
            JsonSerializer.SerializeToElement(
                terminalSnapshot.ObservedGeneration,
                IpcJsonSerializerOptions.StrictPropertyNames),
            terminalRoot.GetProperty("terminalGeneration"));

        var manifestJson = await ReadArtifactTextAsync(
            project.RepositoryRoot,
            GetArtifact(payload, GameViewRecordingArtifactKinds.Manifest));
        var terminalJson = await ReadArtifactTextAsync(
            project.RepositoryRoot,
            GetArtifact(payload, GameViewRecordingArtifactKinds.TerminalRecord));
        Assert.DoesNotContain(ResolveProviderOutputPath(project).Value, manifestJson, StringComparison.Ordinal);
        Assert.DoesNotContain(ResolveProviderOutputPath(project).Value, terminalJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [Trait("Size", "Medium")]
    public async Task FinalizeAsync_WhenArtifactPublicationPrecedesFailedCheckpoint_RepublishesExpectedArtifactAndResumes (
        int failedCheckpoint)
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-finalizer",
            $"resume-checkpoint-{failedCheckpoint}");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(
            scope,
            ProjectFingerprintTestFactory.Create($"game-view-recording-resume-{failedCheckpoint}"),
            unityVersion: "6000.3.11f1");
        var context = new ProjectContext(
            project,
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
        var artifactStore = new FileGameViewRecordingArtifactStore(
            new ImmutableArtifactFilePublisher(static () => CompletedAtUtc.AddSeconds(1)),
            new GameViewRecordingMp4Validator());
        var durableStore = new FileGameViewRecordingExecutionStore();
        using var admissionLease = await AcquireAdmissionLeaseAsync(durableStore, project);
        var lease = Assert.IsAssignableFrom<IGameViewRecordingArtifactLease>(
            artifactStore.Prepare(project, RecordingId, admissionLease).Lease);
        var request = CreateEffectiveRequest();
        var requestRef = Assert.IsType<PathArtifactRef>(
            (await lease.PublishRequestAsync(
                request,
                knownArtifact: null,
                CancellationToken.None)).Artifact);
        var terminalSnapshot = CreateCompletedSnapshot(request);
        var stored = CreateTerminalRecoveryStored(
            project,
            request,
            requestRef,
            terminalSnapshot);
        await durableStore.WriteAsync(
            project,
            lease.ExecutionStatePath,
            stored,
            CancellationToken.None);
        await File.WriteAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            SyntheticGameViewRecordingMp4.Create(),
            CancellationToken.None);
        var faultingStore = new CheckpointFailureExecutionStore(durableStore, failedCheckpoint);

        await Assert.ThrowsAsync<IOException>(async () =>
            await new GameViewRecordingTerminalFinalizer(faultingStore).FinalizeAsync(
                context,
                lease,
                stored,
                terminalSnapshot,
                CancellationToken.None));

        var durableCheckpoint = Assert.IsType<GameViewRecordingStoredExecution>(
            await durableStore.ReadAsync(project, RecordingId, CancellationToken.None));
        var resumed = await new GameViewRecordingTerminalFinalizer(durableStore).FinalizeAsync(
            context,
            lease,
            durableCheckpoint,
            terminalSnapshot,
            CancellationToken.None);

        var resumedSuccess = Assert.IsType<GameViewRecordingTerminalFinalizationSuccess>(resumed);
        var payload = resumedSuccess.Payload;
        using var manifest = await ReadArtifactDocumentAsync(
            project.RepositoryRoot,
            GetArtifact(payload, GameViewRecordingArtifactKinds.Manifest));
        using var terminalRecord = await ReadArtifactDocumentAsync(
            project.RepositoryRoot,
            GetArtifact(payload, GameViewRecordingArtifactKinds.TerminalRecord));
        Assert.DoesNotContain(
            manifest.RootElement.GetProperty("artifactRefs").EnumerateArray(),
            artifact => artifact.GetProperty("kind").GetString()
                == GameViewRecordingArtifactKinds.Manifest.Value);
        Assert.DoesNotContain(
            terminalRecord.RootElement.GetProperty("artifactRefs").EnumerateArray(),
            artifact => artifact.GetProperty("kind").GetString()
                == GameViewRecordingArtifactKinds.TerminalRecord.Value);

        var completedCheckpoint = Assert.IsType<GameViewRecordingStoredExecution>(
            await durableStore.ReadAsync(project, RecordingId, CancellationToken.None));
        var replay = await new GameViewRecordingTerminalFinalizer(durableStore).FinalizeAsync(
            context,
            lease,
            completedCheckpoint,
            terminalSnapshot,
            CancellationToken.None);
        Assert.IsType<GameViewRecordingTerminalFinalizationSuccess>(replay);
    }

    [Theory]
    [InlineData(GameViewRecordingState.Failed)]
    [InlineData(GameViewRecordingState.Indeterminate)]
    [Trait("Size", "Medium")]
    public async Task FinalizeAsync_WithNonCompletedRuntime_RecoversProviderBytesAsPartialOutput (
        GameViewRecordingState runtimeState)
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-finalizer",
            runtimeState.ToString());
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(
            scope,
            ProjectFingerprintTestFactory.Create($"game-view-recording-{runtimeState}"),
            unityVersion: "6000.3.11f1");
        var context = new ProjectContext(
            project,
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
        var artifactStore = new FileGameViewRecordingArtifactStore(
            new ImmutableArtifactFilePublisher(static () => CompletedAtUtc.AddSeconds(1)),
            new GameViewRecordingMp4Validator());
        var executionStore = new FileGameViewRecordingExecutionStore();
        using var admissionLease = await AcquireAdmissionLeaseAsync(executionStore, project);
        var lease = Assert.IsAssignableFrom<IGameViewRecordingArtifactLease>(
            artifactStore.Prepare(project, RecordingId, admissionLease).Lease);
        var request = CreateEffectiveRequest();
        var requestRef = Assert.IsType<PathArtifactRef>(
            (await lease.PublishRequestAsync(
                request,
                knownArtifact: null,
                CancellationToken.None)).Artifact);
        var terminalSnapshot = CreateNonCompletedSnapshot(
            runtimeState,
            request);
        var stored = CreateTerminalRecoveryStored(
            project,
            request,
            requestRef,
            terminalSnapshot);
        await executionStore.WriteAsync(
            project,
            lease.ExecutionStatePath,
            stored,
            CancellationToken.None);
        byte[] providerBytes = [0x01, 0x02, 0x03, 0x04];
        await File.WriteAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            providerBytes,
            CancellationToken.None);

        var result = await new GameViewRecordingTerminalFinalizer(executionStore)
            .FinalizeAsync(
                context,
                lease,
                stored,
                terminalSnapshot,
                CancellationToken.None);

        var success = Assert.IsType<GameViewRecordingTerminalFinalizationSuccess>(result);
        var payload = success.Payload;
        Assert.Equal(runtimeState, payload.TerminalSummary.State);
        Assert.DoesNotContain(
            payload.ArtifactRefs,
            static artifact => artifact.Kind == GameViewRecordingArtifactKinds.Video);
        var partial = GetArtifact(payload, GameViewRecordingArtifactKinds.PartialOutput);
        Assert.Equal(
            providerBytes,
            await File.ReadAllBytesAsync(
                ResolveArtifactPath(project.RepositoryRoot, partial).Value,
                CancellationToken.None));
        Assert.False(File.Exists(ResolveProviderOutputPath(project).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StatusAsync_WhenTerminalPublicationFails_ReturnsTheFinalizerRecoveryCheckpoint ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-finalizer",
            "status-publication-failure");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(
            scope,
            ProjectFingerprintTestFactory.Create("game-view-recording-status-failure"),
            unityVersion: "6000.3.11f1");
        var context = new ProjectContext(
            project,
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
        var artifactStore = new FileGameViewRecordingArtifactStore(
            new ImmutableArtifactFilePublisher(static () => CompletedAtUtc.AddSeconds(1)),
            new GameViewRecordingMp4Validator());
        var executionStore = new FileGameViewRecordingExecutionStore();
        using var admissionLease = await AcquireAdmissionLeaseAsync(executionStore, project);
        var lease = Assert.IsAssignableFrom<IGameViewRecordingArtifactLease>(
            artifactStore.Prepare(project, RecordingId, admissionLease).Lease);
        var request = CreateEffectiveRequest();
        var requestRef = Assert.IsType<PathArtifactRef>(
            (await lease.PublishRequestAsync(
                request,
                knownArtifact: null,
                CancellationToken.None)).Artifact);
        var activeSnapshot = CreateActiveSnapshot(request);
        var active = CreateObservedStored(project, request, requestRef, activeSnapshot);
        await executionStore.WriteAsync(
            project,
            lease.ExecutionStatePath,
            active,
            CancellationToken.None);
        var terminalSnapshot = CreateCompletedSnapshot(request);
        var capability = CreateCapability();
        var requestExecutor = new RecordingRequestExecutor(
            capability,
            terminalSnapshot);
        var terminalFinalizer = new FailingTerminalFinalizer();
        var service = new GameViewRecordingService(
            new FixedProjectContextResolver(context),
            new GameViewRecordingCapabilityResolver(
                new ResolvedRecorderPackageResolver(),
                requestExecutor),
            requestExecutor,
            artifactStore,
            executionStore,
            terminalFinalizer,
            new UnobservableProcessIdentityObserver(),
            new GuidGenerator(),
            TimeProvider.System);

        var result = await service.GetStatusAsync(
            new GameViewRecordingStatusInput(
                ProjectPath: null,
                RecordingId: RecordingId,
                TimeoutMilliseconds: 5_000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.FinalizationFailed, result.Error!.Code);
        Assert.Same(terminalFinalizer.RecoveryPayload, result.ExecutionCheckpoint);
        Assert.Contains(
            result.ExecutionCheckpoint!.ArtifactRefs,
            static artifact => artifact.Kind == GameViewRecordingArtifactKinds.PartialOutput);
    }

    private static GameViewRecordingStoredExecution CreateTerminalRecoveryStored (
        ResolvedUnityProjectContext project,
        GameViewRecordingEffectiveRequest request,
        PathArtifactRef requestRef,
        IpcGameViewRecordingTerminalSnapshot snapshot)
    {
        var preparing = GameViewRecordingPayloadFactory.CreatePreparing(
            project,
            RecordingId,
            request,
            requestRef,
            StartedAtUtc);
        var initial = new GameViewRecordingStoredExecution(
            GameViewRecordingStoredExecution.CurrentSchemaVersion,
            RecordingId,
            new GameViewRecordingRequest(
                request.SchemaVersion,
                request.Resolution,
                request.FrameRate,
                request.MaxDurationSeconds),
            request.CanonicalJson,
            request.Digest,
            requestRef,
            CreateCapability(),
            StartBinding,
            StartDispatchDeadlineUtc,
            runtimeSnapshot: null,
            preparing);
        var recovery = GameViewRecordingPayloadFactory.CreateObservedNonTerminal(
            initial,
            snapshot);
        return new GameViewRecordingStoredExecution(
            initial.SchemaVersion,
            initial.RecordingId,
            initial.Request,
            initial.CanonicalRequestJson,
            initial.RequestDigest,
            initial.RequestRef,
            initial.StartCapability,
            initial.StartBinding,
            initial.StartDispatchDeadlineUtc,
            snapshot,
            recovery);
    }

    private static GameViewRecordingStoredExecution CreateObservedStored (
        ResolvedUnityProjectContext project,
        GameViewRecordingEffectiveRequest request,
        PathArtifactRef requestRef,
        IpcGameViewRecordingActiveSnapshot snapshot)
    {
        var preparing = GameViewRecordingPayloadFactory.CreatePreparing(
            project,
            RecordingId,
            request,
            requestRef,
            StartedAtUtc);
        var initial = new GameViewRecordingStoredExecution(
            GameViewRecordingStoredExecution.CurrentSchemaVersion,
            RecordingId,
            new GameViewRecordingRequest(
                request.SchemaVersion,
                request.Resolution,
                request.FrameRate,
                request.MaxDurationSeconds),
            request.CanonicalJson,
            request.Digest,
            requestRef,
            CreateCapability(),
            StartBinding,
            StartDispatchDeadlineUtc,
            runtimeSnapshot: null,
            preparing);
        var observed = GameViewRecordingPayloadFactory.CreateObservedNonTerminal(initial, snapshot);
        return new GameViewRecordingStoredExecution(
            initial.SchemaVersion,
            initial.RecordingId,
            initial.Request,
            initial.CanonicalRequestJson,
            initial.RequestDigest,
            initial.RequestRef,
            initial.StartCapability,
            initial.StartBinding,
            initial.StartDispatchDeadlineUtc,
            snapshot,
            observed);
    }

    private static GameViewRecordingEffectiveRequest CreateEffectiveRequest ()
    {
        var normalized = GameViewRecordingRequestNormalizer.Normalize(
            new GameViewRecordingRequestDocument(
                GameViewRecordingRequest.CurrentSchemaVersion,
                new PixelDimensions(
                    SyntheticGameViewRecordingMp4.Width,
                    SyntheticGameViewRecordingMp4.Height),
                frameRate: 30,
                UcliOptionalInt32.FromValue(120)),
            minimumWidth: 2,
            maximumWidth: 4096,
            minimumHeight: 2,
            maximumHeight: 4096,
            dimensionMultiple: 2,
            minimumFrameRate: 1,
            maximumFrameRate: 120,
            defaultMaxDurationSeconds: 120,
            maximumMaxDurationSeconds: 600);
        return Assert.IsType<GameViewRecordingEffectiveRequest>(normalized.Request);
    }

    private static IpcGameViewRecordingCompletedSnapshot CreateCompletedSnapshot (
        GameViewRecordingEffectiveRequest request)
    {
        return new IpcGameViewRecordingCompletedSnapshot(
            RecordingId,
            request.Digest,
            GameViewRecordingState.Completed,
            GameViewRecordingStopReason.Manual,
            Runtime,
            CreateRuntimeCleanupAwaitingApplicationFinalization(request),
            new GameViewRecordingTargetObservation(
                "play-mode-view-1",
                "game-view-1",
                display: 0,
                request.Resolution,
                request.Resolution,
                orientation: "upright",
                projectColorSpace: UnityProjectColorSpace.Linear),
            new GameViewRecordingTimingObservation(
                monotonicStartedTimestamp: 100,
                monotonicStopRequestedTimestamp: 190,
                monotonicCompletedTimestamp: 200,
                monotonicFrequency: 100,
                gameTimeStartedSeconds: 1,
                gameTimeCompletedSeconds: 3,
                timeScaleStarted: 1,
                timeScaleCompleted: 1,
                frameCountStarted: 10,
                frameCountCompleted: 70,
                mp4DurationSeconds: null,
                encodedFrameCount: checked((int)SyntheticGameViewRecordingMp4.SampleCount),
                effectiveFrameRate: null,
                droppedFrameCount: null,
                duplicatedFrameCount: null,
                delayedFrameCount: null),
            request.MaxDurationSeconds,
            checked((int)SyntheticGameViewRecordingMp4.SampleCount),
            StartedAtUtc,
            StartedAtUtc.AddSeconds(1),
            CompletedAtUtc,
            CompletedAtUtc,
            StartGeneration,
            new UnityEditorGenerationSnapshot(1, 2, 3, 5));
    }

    private static IpcGameViewRecordingTerminalSnapshot CreateNonCompletedSnapshot (
        GameViewRecordingState state,
        GameViewRecordingEffectiveRequest request)
    {
        return state switch
        {
            GameViewRecordingState.Failed => new IpcGameViewRecordingFailedSnapshot(
                RecordingId,
                request.Digest,
                state,
                GameViewRecordingStopReason.EncoderFailure,
                new IpcError(
                    GameViewRecordingErrorCodes.FinalizationFailed,
                    "Recorder could not finalize the requested output.",
                    InstancePath: null),
                Runtime,
                CreateRuntimeCleanupAwaitingApplicationFinalization(request),
                CreateRuntimeTarget(request),
                CreateRuntimeTiming(encodedFrameCount: null),
                request.MaxDurationSeconds,
                encodedFrameCount: null,
                StartedAtUtc,
                StartedAtUtc.AddSeconds(1),
                CompletedAtUtc,
                CompletedAtUtc,
                StartGeneration,
                new UnityEditorGenerationSnapshot(1, 2, 3, 5)),
            GameViewRecordingState.Indeterminate => new IpcGameViewRecordingIndeterminateSnapshot(
                RecordingId,
                request.Digest,
                state,
                GameViewRecordingStopReason.DomainReload,
                failure: null,
                Runtime,
                cleanup: null,
                target: null,
                timing: null,
                request.MaxDurationSeconds,
                encodedFrameCount: null,
                StartedAtUtc,
                StartedAtUtc.AddSeconds(1),
                CompletedAtUtc,
                CompletedAtUtc,
                StartGeneration,
                new UnityEditorGenerationSnapshot(1, 2, 3, 5)),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "State must be terminal and non-completed."),
        };
    }

    private static IpcGameViewRecordingActiveSnapshot CreateActiveSnapshot (
        GameViewRecordingEffectiveRequest request) =>
        new IpcGameViewRecordingActiveSnapshot(
            RecordingId,
            request.Digest,
            GameViewRecordingState.Recording,
            Runtime,
            CreateRuntimeTarget(request),
            request.MaxDurationSeconds,
            encodedFrameCount: 1,
            StartedAtUtc,
            StartedAtUtc.AddSeconds(1),
            StartGeneration,
            new UnityEditorGenerationSnapshot(1, 2, 3, 4));

    private static GameViewRecordingTargetObservation CreateRuntimeTarget (
        GameViewRecordingEffectiveRequest request) =>
        new(
            "play-mode-view-1",
            "game-view-1",
            display: 0,
            request.Resolution,
            request.Resolution,
            orientation: "upright",
            projectColorSpace: UnityProjectColorSpace.Linear);

    private static GameViewRecordingTimingObservation CreateRuntimeTiming (
        int? encodedFrameCount) =>
        new(
            monotonicStartedTimestamp: 100,
            monotonicStopRequestedTimestamp: 190,
            monotonicCompletedTimestamp: 200,
            monotonicFrequency: 100,
            gameTimeStartedSeconds: 1,
            gameTimeCompletedSeconds: 3,
            timeScaleStarted: 1,
            timeScaleCompleted: 1,
            frameCountStarted: 10,
            frameCountCompleted: 70,
            mp4DurationSeconds: null,
            encodedFrameCount,
            effectiveFrameRate: null,
            droppedFrameCount: null,
            duplicatedFrameCount: null,
            delayedFrameCount: null);

    private static GameViewRecordingCleanupRecord CreateRuntimeCleanupAwaitingApplicationFinalization (
        GameViewRecordingEffectiveRequest request)
    {
        var restorations = Enum.GetValues<GameViewRecordingStateRestorationKind>()
            .Select(static kind => new GameViewRecordingStateRestoration(
                kind,
                beforeValue: null,
                afterValue: null,
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
            request.Digest,
            restorations,
            releases,
            GameViewRecordingCleanupDisposition.Unconfirmed,
            CompletedAtUtc);
    }

    private static GameViewRecordingCapability CreateCapability ()
    {
        var captureProfile = new GameViewRecordingCaptureProfile(
            GameViewRecordingContainer.Mp4,
            GameViewRecordingCodec.H264,
            audio: false,
            alpha: false,
            encodingProfile: "H.264",
            encodingQuality: "high",
            GameViewRecordingTimingMode.ConstantFrameRateCapture);
        return new GameViewRecordingCapability(
            new GameViewRecordingPackageCapability(
                GameViewRecordingPackageState.Resolved,
                GameViewRecorderCompatibilityMetadata.PackageId,
                "5.1.5"),
            new GameViewRecordingCompatibilityCapability(
                GameViewRecordingCompatibilityState.Supported,
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                "5.1.5"),
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.Registered,
                GameViewRecorderCompatibilityMetadata.AdapterId,
                GameViewRecorderCompatibilityMetadata.AdapterVersion),
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Ready,
                blockingCodes: []),
            new GameViewRecordingLimits(2, 4096, 2, 4096, 2, 1, 120, 120, 600),
            captureProfile);
    }

    private static PathArtifactRef GetArtifact (
        GameViewRecordingTerminalPayload payload,
        ArtifactKind kind) =>
        Assert.IsType<PathArtifactRef>(payload.ArtifactRefs.Single(artifact => artifact.Kind == kind));

    private static async Task<T> ReadArtifactAsync<T> (
        AbsolutePath repositoryRoot,
        PathArtifactRef artifact)
        where T : notnull
    {
        var json = await ReadArtifactTextAsync(repositoryRoot, artifact);
        return Assert.IsType<T>(JsonSerializer.Deserialize<T>(json, IpcJsonSerializerOptions.Default));
    }

    private static async Task<JsonDocument> ReadArtifactDocumentAsync (
        AbsolutePath repositoryRoot,
        PathArtifactRef artifact)
    {
        var json = await ReadArtifactTextAsync(repositoryRoot, artifact);
        return JsonDocument.Parse(json);
    }

    private static async Task<string> ReadArtifactTextAsync (
        AbsolutePath repositoryRoot,
        PathArtifactRef artifact)
    {
        var artifactPath = ContainedPath.Create(
            repositoryRoot,
            RootRelativePath.Parse(artifact.Path.Value)).Target;
        return await File.ReadAllTextAsync(artifactPath.Value, CancellationToken.None);
    }

    private static AbsolutePath ResolveArtifactPath (
        AbsolutePath repositoryRoot,
        PathArtifactRef artifact) =>
        ContainedPath.Create(
            repositoryRoot,
            RootRelativePath.Parse(artifact.Path.Value)).Target;

    private static AbsolutePath ResolveProviderOutputPath (
        ResolvedUnityProjectContext project) =>
        UcliStoragePathResolver.ResolveGameViewRecordingProviderOutputPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            RecordingId);

    private static async ValueTask<IGameViewRecordingAdmissionLease> AcquireAdmissionLeaseAsync (
        FileGameViewRecordingExecutionStore executionStore,
        ResolvedUnityProjectContext project)
    {
        var lease = await executionStore.TryAcquireAdmissionLeaseAsync(
            project,
            RecordingId,
            StartBinding,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        return Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(lease);
    }

    private static void AssertJsonEquivalent (JsonElement expected, JsonElement actual)
    {
        Assert.Equal(
            Rfc8785JsonCanonicalizer.Canonicalize(expected),
            Rfc8785JsonCanonicalizer.Canonicalize(actual));
    }

    private sealed class FixedProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContext context;

        public FixedProjectContextResolver (ProjectContext context)
        {
            this.context = context;
        }

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
            AbsolutePath? projectPath,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ProjectContextResolutionResult.Success(context));
    }

    private sealed class ResolvedRecorderPackageResolver : IGameViewRecorderPackageResolver
    {
        public ValueTask<GameViewRecorderPackageResolution> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GameViewRecorderPackageResolution.Resolved("5.1.5"));
    }

    private sealed class RecordingRequestExecutor : IUnityRequestExecutor
    {
        private readonly GameViewRecordingCapability capability;
        private readonly IpcGameViewRecordingTerminalSnapshot terminalSnapshot;

        public RecordingRequestExecutor (
            GameViewRecordingCapability capability,
            IpcGameViewRecordingTerminalSnapshot terminalSnapshot)
        {
            this.capability = capability;
            this.terminalSnapshot = terminalSnapshot;
        }

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            var responsePayload = payload switch
            {
                UnityRequestPayload.RecordingCapability => IpcPayloadCodec.SerializeToElement(
                    new IpcGameViewRecordingCapabilityResponse(
                        capability.Adapter,
                        capability.RuntimeAdmission,
                        capability.Limits,
                        capability.CaptureProfile,
                        StartBinding,
                        observedRuntime: StartBinding.Runtime)),
                UnityRequestPayload.RecordingStatus => IpcPayloadCodec.SerializeToElement(
                    new IpcGameViewRecordingStatusResponse(
                        new IpcSelectedGameViewRecordingSelection(terminalSnapshot))),
                _ => throw new InvalidOperationException(
                    $"Unexpected recording request payload: {payload.GetType().Name}."),
            };
            return ValueTask.FromResult(UnityRequestExecutionResult.Success(
                new UnityRequestResponse(responsePayload, Errors: [])));
        }
    }

    private sealed class FailingTerminalFinalizer : IGameViewRecordingTerminalFinalizer
    {
        public GameViewRecordingRecoveryPayload? RecoveryPayload { get; private set; }

        public ValueTask<GameViewRecordingTerminalFinalizationResult> FinalizeAsync (
            ProjectContext context,
            IGameViewRecordingArtifactLease artifactLease,
            GameViewRecordingStoredExecution stored,
            IpcGameViewRecordingTerminalSnapshot terminalSnapshot,
            CancellationToken cancellationToken = default)
        {
            var recovery = Assert.IsType<GameViewRecordingRecoveryPayload>(stored.Payload);
            var partial = new PathArtifactRef(
                GameViewRecordingArtifactKinds.PartialOutput,
                GameViewRecordingArtifactMediaTypes.Binary,
                new ArtifactPath(".ucli/recordings/partial.mp4"),
                MackySoft.Ucli.Contracts.Cryptography.Sha256Digest.Compute([1, 2, 3]),
                sizeBytes: 3,
                CompletedAtUtc);
            RecoveryPayload = new GameViewRecordingRecoveryPayload(
                recovery.Project,
                recovery.ExecutionRef,
                recovery.RequestDigest,
                recovery.RequestRef,
                recovery.Progress,
                [.. recovery.ArtifactRefs, partial],
                recovery.Diagnostics);
            return ValueTask.FromResult(GameViewRecordingTerminalFinalizationResult.Failure(
                RecoveryPayload,
                ExecutionError.InternalError(
                    "Terminal artifact publication failed.",
                    GameViewRecordingErrorCodes.FinalizationFailed)));
        }
    }

    private sealed class UnobservableProcessIdentityObserver : IProcessIdentityObserver
    {
        public ProcessIdentityStatus Observe (ProcessIdentity process) =>
            ProcessIdentityStatus.Unobservable;
    }

    private sealed class CheckpointFailureExecutionStore : IGameViewRecordingExecutionStore
    {
        private readonly IGameViewRecordingExecutionStore inner;
        private readonly int failedWrite;
        private int writeCount;

        public CheckpointFailureExecutionStore (
            IGameViewRecordingExecutionStore inner,
            int failedWrite)
        {
            this.inner = inner;
            this.failedWrite = failedWrite;
        }

        public ValueTask<GameViewRecordingStoredExecution?> ReadAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(project, recordingId, cancellationToken);

        public ValueTask<GameViewRecordingStoredExecution?> ReadCurrentAsync (
            ResolvedUnityProjectContext project,
            Guid runtimeId,
            CancellationToken cancellationToken = default) =>
            inner.ReadCurrentAsync(project, runtimeId, cancellationToken);

        public ValueTask WriteAsync (
            ResolvedUnityProjectContext project,
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution execution,
            CancellationToken cancellationToken = default)
        {
            writeCount++;
            if (writeCount == failedWrite)
            {
                throw new IOException("Injected failure after immutable artifact publication.");
            }

            return inner.WriteAsync(project, executionStatePath, execution, cancellationToken);
        }

        public ValueTask<GameViewRecordingCheckpointExchangeResult> CompareExchangeAsync (
            ResolvedUnityProjectContext project,
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution expected,
            GameViewRecordingStoredExecution replacement,
            CancellationToken cancellationToken = default) =>
            inner.CompareExchangeAsync(
                project,
                executionStatePath,
                expected,
                replacement,
                cancellationToken);

        public ValueTask<IGameViewRecordingAdmissionLease?> TryAcquireAdmissionLeaseAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            IpcGameViewRecordingStartBinding startBinding,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            inner.TryAcquireAdmissionLeaseAsync(
                project,
                recordingId,
                startBinding,
                timeout,
                cancellationToken);

        public ValueTask<IGameViewRecordingTerminalPublicationLease?> TryAcquireTerminalPublicationLeaseAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            inner.TryAcquireTerminalPublicationLeaseAsync(
                project,
                recordingId,
                timeout,
                cancellationToken);

    }
}
