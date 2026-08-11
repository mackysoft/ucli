using System.Text;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Features.Recording.Artifacts;
using MackySoft.Ucli.Features.Recording.Artifacts.Mp4;
using MackySoft.Ucli.Features.Recording.Registry;
using MackySoft.Ucli.Infrastructure.Artifacts;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Features.Recording.Artifacts.Mp4;

namespace MackySoft.Ucli.Tests.Features.Recording.Artifacts;

public sealed class FileGameViewRecordingArtifactStoreTests
{
    private static readonly Guid RecordingId =
        Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly IpcGameViewRecordingStartBinding StartBinding = new(
        new ProcessIdentity(ProcessId: 1234, Generation: 5678),
        new GameViewRecordingRuntimeIdentity(
            Guid.Parse("6ce62a0f-e715-447b-bd14-d80acfac8b35"),
            "windows",
            "media-foundation",
            "1"),
        new UnityEditorGenerationSnapshot(1, 2, 3, 4));

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PrepareAndOpen_PublishCanonicalJsonAndPreserveDurableExecutionState ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "canonical-json");
        var project = CreateProject(scope);
        var request = CreateEffectiveRequest();
        var store = CreateStore();
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var preparation = store.Prepare(project, RecordingId, admissionLease);
        var lease = AssertPrepared(preparation);
        var stateBytes = Encoding.UTF8.GetBytes("durable-state-sentinel");
        await File.WriteAllBytesAsync(
            lease.ExecutionStatePath.Value,
            stateBytes,
            CancellationToken.None);

        var requestResult = await lease.PublishRequestAsync(
            request,
            knownArtifact: null,
            CancellationToken.None);
        var requestArtifact = AssertPublished(requestResult);
        var cleanup = CreateCleanup(request);
        var cleanupResult = await lease.PublishCleanupAsync(
            cleanup,
            knownArtifact: null,
            CancellationToken.None);
        var cleanupArtifact = AssertPublished(cleanupResult);

        Assert.Equal(request.Digest, requestArtifact.Digest);
        AssertArtifactMeasurement(project.RepositoryRoot, requestArtifact);
        AssertArtifactMeasurement(project.RepositoryRoot, cleanupArtifact);
        await AssertCanonicalContractAsync<GameViewRecordingRequest>(
            ResolveArtifactPath(project.RepositoryRoot, requestArtifact));
        await AssertCanonicalContractAsync<GameViewRecordingCleanupRecord>(
            ResolveArtifactPath(project.RepositoryRoot, cleanupArtifact));

        var opened = store.Open(project, RecordingId);
        var reopenedLease = Assert.IsAssignableFrom<IGameViewRecordingArtifactLease>(opened.Lease);
        Assert.True(opened.IsSuccess);
        Assert.Equal(
            stateBytes,
            await File.ReadAllBytesAsync(reopenedLease.ExecutionStatePath.Value, CancellationToken.None));
        var replay = await reopenedLease.PublishCleanupAsync(
            cleanup,
            cleanupArtifact,
            CancellationToken.None);
        Assert.Equal(cleanupArtifact, AssertPublished(replay));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishRequestAsync_WhenExpectedDestinationExistsWithoutDurableReference_RepublishesWithANewReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "unknown-existing-json");
        var project = CreateProject(scope);
        var firstPublishedAtUtc = new DateTimeOffset(2026, 8, 5, 1, 2, 3, TimeSpan.Zero);
        var republishedAtUtc = firstPublishedAtUtc.AddSeconds(1);
        var publicationTimes = new Queue<DateTimeOffset>([firstPublishedAtUtc, republishedAtUtc]);
        var store = new FileGameViewRecordingArtifactStore(
            new ImmutableArtifactFilePublisher(publicationTimes.Dequeue),
            new GameViewRecordingMp4Validator());
        var request = CreateEffectiveRequest();
        PathArtifactRef artifact;
        AbsolutePath orphanedArtifactCandidate;
        AbsolutePath orphanedStateCandidate;
        using (var initialAdmissionLease = await AcquireAdmissionLeaseAsync(project))
        {
            var initialLease = AssertPrepared(
                store.Prepare(project, RecordingId, initialAdmissionLease));
            var first = await initialLease.PublishRequestAsync(
                request,
                knownArtifact: null,
                CancellationToken.None);
            artifact = AssertPublished(first);
            orphanedArtifactCandidate = ContainedPath.Create(
                UcliStoragePathResolver.ResolveGameViewRecordingArtifactDirectory(
                    project.RepositoryRoot,
                    project.ProjectFingerprint,
                    RecordingId),
                RootRelativePath.Parse(
                    FileUtilities.AtomicWriteTemporaryFileNamePrefix + "orphaned-artifact"))
                .Target;
            orphanedStateCandidate = ContainedPath.Create(
                UcliStoragePathResolver.ResolveGameViewRecordingExecutionWorkDirectory(
                    project.RepositoryRoot,
                    project.ProjectFingerprint,
                    RecordingId),
                RootRelativePath.Parse(
                    FileUtilities.AtomicWriteTemporaryFileNamePrefix + "orphaned-state"))
                .Target;
            await File.WriteAllBytesAsync(
                orphanedArtifactCandidate.Value,
                [1],
                CancellationToken.None);
            await File.WriteAllBytesAsync(
                orphanedStateCandidate.Value,
                [2],
                CancellationToken.None);
        }

        using var recoveryAdmissionLease = await AcquireAdmissionLeaseAsync(project);
        var recoveredLease = AssertPrepared(
            store.Prepare(project, RecordingId, recoveryAdmissionLease));
        var replayWithoutReference = await recoveredLease.PublishRequestAsync(
            request,
            knownArtifact: null,
            CancellationToken.None);

        var recovered = AssertPublished(replayWithoutReference);
        Assert.Equal(artifact.Kind, recovered.Kind);
        Assert.Equal(artifact.MediaType, recovered.MediaType);
        Assert.Equal(artifact.Path, recovered.Path);
        Assert.Equal(artifact.Digest, recovered.Digest);
        Assert.Equal(artifact.SizeBytes, recovered.SizeBytes);
        Assert.Equal(firstPublishedAtUtc, artifact.CreatedAtUtc);
        Assert.Equal(republishedAtUtc, recovered.CreatedAtUtc);
        Assert.False(File.Exists(orphanedArtifactCandidate.Value));
        Assert.False(File.Exists(orphanedStateCandidate.Value));
        AssertArtifactMeasurement(project.RepositoryRoot, recovered);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishRequestAsync_WhenUnreferencedDestinationContainsDifferentCanonicalDocument_RejectsRepublishing ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "foreign-existing-json");
        var project = CreateProject(scope);
        var store = CreateStore();
        var request = CreateEffectiveRequest();
        PathArtifactRef artifact;
        using (var initialAdmissionLease = await AcquireAdmissionLeaseAsync(project))
        {
            var initialLease = AssertPrepared(
                store.Prepare(project, RecordingId, initialAdmissionLease));
            artifact = AssertPublished(await initialLease.PublishRequestAsync(
                request,
                knownArtifact: null,
                CancellationToken.None));
        }

        var artifactPath = ResolveArtifactPath(project.RepositoryRoot, artifact);
        var foreignRequest = CreateEffectiveRequest(maxDurationSeconds: 60);
        await File.WriteAllTextAsync(
            artifactPath.Value,
            foreignRequest.CanonicalJson,
            Encoding.UTF8,
            CancellationToken.None);

        using var recoveryAdmissionLease = await AcquireAdmissionLeaseAsync(project);
        var recoveryLease = AssertPrepared(
            store.Prepare(project, RecordingId, recoveryAdmissionLease));
        var recovery = await recoveryLease.PublishRequestAsync(
            request,
            knownArtifact: null,
            CancellationToken.None);

        Assert.False(recovery.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.FinalizationFailed, recovery.Error!.Code);
        Assert.Equal(
            foreignRequest.CanonicalJson,
            await File.ReadAllTextAsync(artifactPath.Value, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DiscardUnregisteredArtifactsAsync_WithRejectedStartLayout_AllowsSameIdentifierToBePreparedAgain ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "discard-unregistered-request");
        var project = CreateProject(scope);
        var store = CreateStore();
        GameViewRecordingArtifactDiscardResult discard;
        PathArtifactRef requestArtifact;
        using (var admissionLease = await AcquireAdmissionLeaseAsync(project))
        {
            var lease = AssertPrepared(store.Prepare(project, RecordingId, admissionLease));
            var requestResult = await lease.PublishRequestAsync(
                CreateEffectiveRequest(),
                knownArtifact: null,
                CancellationToken.None);
            requestArtifact = AssertPublished(requestResult);
            await File.WriteAllBytesAsync(
                ResolveExecutionStateLockPath(project).Value,
                [0],
                CancellationToken.None);
            discard = await lease.DiscardUnregisteredArtifactsAsync(
                requestArtifact,
                CancellationToken.None);
        }

        Assert.True(discard.IsSuccess, discard.Error?.Message);
        Assert.False(File.Exists(ResolveArtifactPath(project.RepositoryRoot, requestArtifact).Value));
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveGameViewRecordingArtifactDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            RecordingId).Value));
        Assert.True(ResolveProviderOutputPath(project).TryGetParent(out var providerDirectory));
        Assert.False(Directory.Exists(providerDirectory.Value));
        Assert.False(File.Exists(ResolveExecutionStateLockPath(project).Value));
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveGameViewRecordingExecutionWorkDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            RecordingId).Value));

        using var retryAdmissionLease = await AcquireAdmissionLeaseAsync(project);
        var retry = store.Prepare(project, RecordingId, retryAdmissionLease);

        Assert.True(retry.IsSuccess, retry.Error?.Message);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DiscardUnregisteredArtifactsAsync_WhenAnotherArtifactExists_PreservesArtifactScope ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "preserve-registered-artifact-layout");
        var project = CreateProject(scope);
        var request = CreateEffectiveRequest();
        var store = CreateStore();
        PathArtifactRef requestArtifact;
        PathArtifactRef cleanupArtifact;
        GameViewRecordingArtifactDiscardResult discard;
        using (var admissionLease = await AcquireAdmissionLeaseAsync(project))
        {
            var lease = AssertPrepared(store.Prepare(project, RecordingId, admissionLease));
            requestArtifact = AssertPublished(await lease.PublishRequestAsync(
                request,
                knownArtifact: null,
                CancellationToken.None));
            cleanupArtifact = AssertPublished(await lease.PublishCleanupAsync(
                CreateCleanup(request),
                knownArtifact: null,
                CancellationToken.None));
            discard = await lease.DiscardUnregisteredArtifactsAsync(
                requestArtifact,
                CancellationToken.None);
        }

        Assert.False(discard.IsSuccess);
        Assert.True(File.Exists(ResolveArtifactPath(project.RepositoryRoot, requestArtifact).Value));
        Assert.True(File.Exists(ResolveArtifactPath(project.RepositoryRoot, cleanupArtifact).Value));
        using var recoveryAdmissionLease = await AcquireAdmissionLeaseAsync(project);
        var recovery = store.Prepare(project, RecordingId, recoveryAdmissionLease);
        Assert.False(recovery.IsSuccess);
        Assert.True(File.Exists(ResolveArtifactPath(project.RepositoryRoot, requestArtifact).Value));
        Assert.True(File.Exists(ResolveArtifactPath(project.RepositoryRoot, cleanupArtifact).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishVideoAsync_WithFinalizedProviderOutput_PublishesValidatedBytesAndSupportsRecoveryAfterCleanup ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "video-publication");
        var project = CreateProject(scope);
        var store = CreateStore();
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(store.Prepare(project, RecordingId, admissionLease));
        var request = CreateEffectiveRequest();
        var stateBytes = Encoding.UTF8.GetBytes("execution-state");
        await File.WriteAllBytesAsync(lease.ExecutionStatePath.Value, stateBytes, CancellationToken.None);
        var mp4Bytes = SyntheticGameViewRecordingMp4.Create();
        await File.WriteAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            mp4Bytes,
            CancellationToken.None);

        var publicationResult = await lease.PublishVideoAsync(
            request,
            observedEncodedFrameCount: checked((int)SyntheticGameViewRecordingMp4.SampleCount),
            knownArtifact: null,
            CancellationToken.None);
        var publication = Assert.IsType<GameViewRecordingVideoPublication>(publicationResult.Publication);
        Assert.True(publicationResult.IsSuccess);
        Assert.Equal((ulong)SyntheticGameViewRecordingMp4.SampleCount, publication.EncodedFrameCount);
        Assert.Equal(2, publication.DurationSeconds);
        Assert.Equal(30, publication.EffectiveFrameRate);
        Assert.Equal(
            mp4Bytes,
            await File.ReadAllBytesAsync(
                ResolveArtifactPath(project.RepositoryRoot, publication.Artifact).Value,
                CancellationToken.None));

        var cleanup = lease.CleanupProviderOutput();
        Assert.True(cleanup.IsSuccess);
        Assert.False(File.Exists(ResolveProviderOutputPath(project).Value));
        Assert.True(File.Exists(lease.ExecutionStatePath.Value));
        Assert.Equal(
            stateBytes,
            await File.ReadAllBytesAsync(lease.ExecutionStatePath.Value, CancellationToken.None));

        var reopenedLease = Assert.IsAssignableFrom<IGameViewRecordingArtifactLease>(
            store.Open(project, RecordingId).Lease);
        var replay = await reopenedLease.PublishVideoAsync(
            request,
            observedEncodedFrameCount: checked((int)SyntheticGameViewRecordingMp4.SampleCount),
            publication.Artifact,
            CancellationToken.None);
        Assert.True(replay.IsSuccess);
        Assert.Equal(publication.Artifact, replay.Publication!.Artifact);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishVideoAsync_WhenProviderStagingExceedsSampleLimit_FailsFinalization ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "staging-sample-limit");
        var project = CreateProject(scope);
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(CreateStore().Prepare(project, RecordingId, admissionLease));
        var bytes = CreateSampleLimitExceedingMp4();
        await File.WriteAllBytesAsync(ResolveProviderOutputPath(project).Value, bytes, CancellationToken.None);

        var result = await lease.PublishVideoAsync(
            CreateEffectiveRequest(maxDurationSeconds: 2),
            observedEncodedFrameCount: checked((int)SyntheticGameViewRecordingMp4.SampleCount + 1),
            knownArtifact: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.FinalizationFailed, result.Error!.Code);
        Assert.True(File.Exists(ResolveProviderOutputPath(project).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishVideoAsync_WhenUncheckpointedArtifactExceedsSampleLimit_FailsFinalization ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "uncheckpointed-sample-limit");
        var project = CreateProject(scope);
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(CreateStore().Prepare(project, RecordingId, admissionLease));
        var bytes = CreateSampleLimitExceedingMp4();
        var destination = UcliStoragePathResolver.ResolveGameViewRecordingVideoArtifactPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            RecordingId);
        await File.WriteAllBytesAsync(destination.Value, bytes, CancellationToken.None);
        await File.WriteAllBytesAsync(ResolveProviderOutputPath(project).Value, bytes, CancellationToken.None);

        var result = await lease.PublishVideoAsync(
            CreateEffectiveRequest(maxDurationSeconds: 2),
            observedEncodedFrameCount: checked((int)SyntheticGameViewRecordingMp4.SampleCount + 1),
            knownArtifact: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.FinalizationFailed, result.Error!.Code);
        Assert.True(File.Exists(destination.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishVideoAsync_WhenCheckpointedArtifactExceedsSampleLimit_FailsFinalization ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "checkpointed-sample-limit");
        var project = CreateProject(scope);
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(CreateStore().Prepare(project, RecordingId, admissionLease));
        var bytes = CreateSampleLimitExceedingMp4();
        await File.WriteAllBytesAsync(ResolveProviderOutputPath(project).Value, bytes, CancellationToken.None);
        var publication = Assert.IsType<GameViewRecordingVideoPublication>(
            (await lease.PublishVideoAsync(
                CreateEffectiveRequest(maxDurationSeconds: 3),
                observedEncodedFrameCount: checked((int)SyntheticGameViewRecordingMp4.SampleCount + 1),
                knownArtifact: null,
                CancellationToken.None)).Publication);

        var result = await lease.PublishVideoAsync(
            CreateEffectiveRequest(maxDurationSeconds: 2),
            observedEncodedFrameCount: checked((int)SyntheticGameViewRecordingMp4.SampleCount + 1),
            publication.Artifact,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.FinalizationFailed, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishVideoAsync_WhenProviderOutputIsNotFinalized_DoesNotPublishVideo ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "invalid-video");
        var project = CreateProject(scope);
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(CreateStore().Prepare(project, RecordingId, admissionLease));
        await File.WriteAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            SyntheticGameViewRecordingMp4.CreateTruncatedMovie(),
            CancellationToken.None);

        var result = await lease.PublishVideoAsync(
            CreateEffectiveRequest(),
            observedEncodedFrameCount: null,
            knownArtifact: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.FinalizationFailed, result.Error!.Code);
        Assert.True(File.Exists(ResolveProviderOutputPath(project).Value));
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveGameViewRecordingVideoArtifactPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            RecordingId).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishVideoAsync_WhenUnreferencedDestinationDiffersFromProviderOutput_RejectsRepublishing ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "foreign-existing-video");
        var project = CreateProject(scope);
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(CreateStore().Prepare(project, RecordingId, admissionLease));
        var providerBytes = SyntheticGameViewRecordingMp4.Create();
        await File.WriteAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            providerBytes,
            CancellationToken.None);
        var publication = Assert.IsType<GameViewRecordingVideoPublication>(
            (await lease.PublishVideoAsync(
                CreateEffectiveRequest(),
                checked((int)SyntheticGameViewRecordingMp4.SampleCount),
                knownArtifact: null,
                CancellationToken.None)).Publication);
        var destination = ResolveArtifactPath(project.RepositoryRoot, publication.Artifact);
        var foreignBytes = providerBytes.ToArray();
        foreignBytes[^1] ^= 0x01;
        await File.WriteAllBytesAsync(destination.Value, foreignBytes, CancellationToken.None);

        var recovery = await lease.PublishVideoAsync(
            CreateEffectiveRequest(),
            checked((int)SyntheticGameViewRecordingMp4.SampleCount),
            knownArtifact: null,
            CancellationToken.None);

        Assert.False(recovery.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.FinalizationFailed, recovery.Error!.Code);
        Assert.Equal(foreignBytes, await File.ReadAllBytesAsync(destination.Value, CancellationToken.None));
        Assert.Equal(providerBytes, await File.ReadAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RecoverPartialOutputAsync_WhenProviderCreatedNoOutput_ReportsKnownAbsence ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "partial-output-absent");
        var project = CreateProject(scope);
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(CreateStore().Prepare(project, RecordingId, admissionLease));

        var result = await lease.RecoverPartialOutputAsync(
            knownArtifact: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.IsAbsent);
        Assert.Null(result.Artifact);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupProviderOutput_WhenProviderDirectoryContainsForeignEntry_PreservesEveryEntry ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-artifacts",
            "safe-cleanup");
        var project = CreateProject(scope);
        using var admissionLease = await AcquireAdmissionLeaseAsync(project);
        var lease = AssertPrepared(CreateStore().Prepare(project, RecordingId, admissionLease));
        await File.WriteAllBytesAsync(
            ResolveProviderOutputPath(project).Value,
            [1, 2, 3],
            CancellationToken.None);
        Assert.True(ResolveProviderOutputPath(project).TryGetParent(out var providerDirectory));
        var foreignPath = ContainedPath.Create(
            providerDirectory,
            RootRelativePath.Parse("foreign.txt")).Target;
        await File.WriteAllTextAsync(foreignPath.Value, "foreign", CancellationToken.None);

        var cleanup = lease.CleanupProviderOutput();

        Assert.False(cleanup.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.CleanupFailed, cleanup.Error!.Code);
        Assert.True(File.Exists(ResolveProviderOutputPath(project).Value));
        Assert.Equal("foreign", await File.ReadAllTextAsync(foreignPath.Value, CancellationToken.None));
    }

    private static FileGameViewRecordingArtifactStore CreateStore ()
    {
        return new FileGameViewRecordingArtifactStore(
            new ImmutableArtifactFilePublisher(static () => DateTimeOffset.UtcNow),
            new GameViewRecordingMp4Validator());
    }

    private static byte[] CreateSampleLimitExceedingMp4 ()
    {
        return SyntheticGameViewRecordingMp4.Create(
            timeToSampleEntries:
            [
                (SyntheticGameViewRecordingMp4.SampleCount + 1, SyntheticGameViewRecordingMp4.SampleDelta),
            ]);
    }

    private static async ValueTask<IGameViewRecordingAdmissionLease> AcquireAdmissionLeaseAsync (
        ResolvedUnityProjectContext project)
    {
        var lease = await new FileGameViewRecordingExecutionStore()
            .TryAcquireAdmissionLeaseAsync(
                project,
                RecordingId,
                StartBinding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None);
        return Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(lease);
    }

    private static ResolvedUnityProjectContext CreateProject (TestDirectoryScope scope)
    {
        return ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(
            scope,
            ProjectFingerprintTestFactory.Create("game-view-recording-artifact-store"));
    }

    private static GameViewRecordingEffectiveRequest CreateEffectiveRequest (
        int maxDurationSeconds = 120)
    {
        var result = GameViewRecordingRequestNormalizer.Normalize(
            new GameViewRecordingRequestDocument(
                GameViewRecordingRequest.CurrentSchemaVersion,
                new PixelDimensions(
                    SyntheticGameViewRecordingMp4.Width,
                    SyntheticGameViewRecordingMp4.Height),
                frameRate: 30,
                UcliOptionalInt32.FromValue(maxDurationSeconds)),
            minimumWidth: 2,
            maximumWidth: 4096,
            minimumHeight: 2,
            maximumHeight: 4096,
            dimensionMultiple: 2,
            minimumFrameRate: 1,
            maximumFrameRate: 120,
            defaultMaxDurationSeconds: 120,
            maximumMaxDurationSeconds: 600);
        return Assert.IsType<GameViewRecordingEffectiveRequest>(result.Request);
    }

    private static GameViewRecordingCleanupRecord CreateCleanup (
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
            .Select(static kind => new GameViewRecordingResourceRelease(
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
            GameViewRecordingCleanupDisposition.Complete,
            DateTimeOffset.UtcNow);
    }

    private static IGameViewRecordingArtifactLease AssertPrepared (
        GameViewRecordingArtifactPreparationResult preparation)
    {
        Assert.True(preparation.IsSuccess, preparation.Error?.Message);
        return Assert.IsAssignableFrom<IGameViewRecordingArtifactLease>(preparation.Lease);
    }

    private static PathArtifactRef AssertPublished (
        GameViewRecordingArtifactPublicationResult publication)
    {
        Assert.True(publication.IsSuccess, publication.Error?.Message);
        return Assert.IsType<PathArtifactRef>(publication.Artifact);
    }

    private static void AssertArtifactMeasurement (
        AbsolutePath repositoryRoot,
        PathArtifactRef artifact)
    {
        var artifactPath = ResolveArtifactPath(repositoryRoot, artifact);
        var bytes = File.ReadAllBytes(artifactPath.Value);
        Assert.Equal(checked((ulong)bytes.LongLength), artifact.SizeBytes);
        Assert.Equal(Sha256Digest.Compute(bytes), artifact.Digest);
    }

    private static async Task AssertCanonicalContractAsync<T> (AbsolutePath artifactPath)
    {
        var bytes = await File.ReadAllBytesAsync(artifactPath.Value, CancellationToken.None);
        Assert.Equal(bytes, Rfc8785JsonCanonicalizer.Canonicalize(bytes));
        Assert.NotNull(JsonSerializer.Deserialize<T>(bytes, IpcJsonSerializerOptions.StrictPropertyNames));
    }

    private static AbsolutePath ResolveArtifactPath (
        AbsolutePath repositoryRoot,
        PathArtifactRef artifact)
    {
        return ContainedPath.Create(
            repositoryRoot,
            RootRelativePath.Parse(artifact.Path.Value)).Target;
    }

    private static AbsolutePath ResolveExecutionStateLockPath (
        ResolvedUnityProjectContext project) =>
        UcliStoragePathResolver.ResolveGameViewRecordingExecutionStateLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            RecordingId);

    private static AbsolutePath ResolveProviderOutputPath (
        ResolvedUnityProjectContext project) =>
        UcliStoragePathResolver.ResolveGameViewRecordingProviderOutputPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            RecordingId);
}
