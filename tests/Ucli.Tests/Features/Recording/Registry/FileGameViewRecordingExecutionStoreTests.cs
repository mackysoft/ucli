using MackySoft.Ucli.Application.Features.Recording.Projection;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Features.Recording.Registry;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Features.Recording.Registry;

public sealed class FileGameViewRecordingExecutionStoreTests
{
    private static readonly DateTimeOffset ObservedAtUtc =
        new(2026, 8, 5, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Admission_DifferentRuntimesInOneProject_CanRegisterAndSelectIndependently ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-registry",
            "different-runtimes");
        var project = CreateProject(scope);
        var store = new FileGameViewRecordingExecutionStore();
        var firstRecordingId = Guid.Parse("f81149cb-dd53-4506-9710-b088454e2e38");
        var secondRecordingId = Guid.Parse("3689856c-cd05-41d4-a4c4-8e175989db93");
        var firstBinding = CreateStartBinding(
            Guid.Parse("db0918ac-f7c0-49d6-baf2-4a505ad0facd"),
            processId: 101);
        var secondBinding = CreateStartBinding(
            Guid.Parse("8c934e72-0af5-42c1-864f-cb2c5cd79230"),
            processId: 202);

        using var firstLease = Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(
            await store.TryAcquireAdmissionLeaseAsync(
                project,
                firstRecordingId,
                firstBinding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None));
        using var secondLease = Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(
            await store.TryAcquireAdmissionLeaseAsync(
                project,
                secondRecordingId,
                secondBinding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None));
        var first = CreateStored(project, firstRecordingId, firstBinding);
        var second = CreateStored(project, secondRecordingId, secondBinding);

        var firstRegistration = await firstLease.TryRegisterAsync(
            ResolveStatePath(project, firstRecordingId),
            first,
            CancellationToken.None);
        var secondRegistration = await secondLease.TryRegisterAsync(
            ResolveStatePath(project, secondRecordingId),
            second,
            CancellationToken.None);

        Assert.True(firstRegistration.Registered);
        Assert.True(secondRegistration.Registered);
        Assert.Equal(firstBinding, firstLease.StartBinding);
        Assert.Equal(secondBinding, secondLease.StartBinding);
        Assert.Equal(
            firstRecordingId,
            (await store.ReadCurrentAsync(
                project,
                firstBinding.Runtime.RuntimeId,
                CancellationToken.None))!.RecordingId);
        Assert.Equal(
            secondRecordingId,
            (await store.ReadCurrentAsync(
                project,
                secondBinding.Runtime.RuntimeId,
                CancellationToken.None))!.RecordingId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Admission_SameRuntime_ExcludesConcurrentLeaseAndRejectsASecondNonTerminalExecution ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-registry",
            "same-runtime");
        var project = CreateProject(scope);
        var store = new FileGameViewRecordingExecutionStore();
        var firstRecordingId = Guid.Parse("f4a158fb-c015-4856-8a4b-b82875c439f9");
        var secondRecordingId = Guid.Parse("1ecdcac8-4a24-4fd3-b5df-510c53089d02");
        var binding = CreateStartBinding(
            Guid.Parse("be15a289-f87e-479d-9ee7-92ff2a060885"),
            processId: 303);

        using (var firstLease = Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(
            await store.TryAcquireAdmissionLeaseAsync(
                project,
                firstRecordingId,
                binding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)))
        {
            var concurrent = await store.TryAcquireAdmissionLeaseAsync(
                project,
                secondRecordingId,
                binding,
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None);
            Assert.Null(concurrent);

            var first = CreateStored(project, firstRecordingId, binding);
            Assert.True((await firstLease.TryRegisterAsync(
                ResolveStatePath(project, firstRecordingId),
                first,
                CancellationToken.None)).Registered);
        }

        using var secondLease = Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(
            await store.TryAcquireAdmissionLeaseAsync(
                project,
                secondRecordingId,
                binding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None));
        var second = CreateStored(project, secondRecordingId, binding);

        var registration = await secondLease.TryRegisterAsync(
            ResolveStatePath(project, secondRecordingId),
            second,
            CancellationToken.None);

        Assert.False(registration.Registered);
        Assert.Equal(firstRecordingId, registration.Existing!.RecordingId);
        Assert.Equal(
            firstRecordingId,
            (await store.ReadCurrentAsync(
                project,
                binding.Runtime.RuntimeId,
                CancellationToken.None))!.RecordingId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Admission_SameRecordingIdAcrossDifferentRuntimes_RegistersOneDurableExecution ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-registry",
            "same-recording-id");
        var project = CreateProject(scope);
        var store = new FileGameViewRecordingExecutionStore();
        var recordingId = Guid.Parse("087eeff5-f7a3-4297-9c54-0946699bc6eb");
        var firstBinding = CreateStartBinding(
            Guid.Parse("e993a72e-acdd-4a03-8e87-05029397e324"),
            processId: 501);
        var secondBinding = CreateStartBinding(
            Guid.Parse("2bfffb71-74e2-4475-abcb-c7625d707e5c"),
            processId: 502);
        using var firstLease = Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(
            await store.TryAcquireAdmissionLeaseAsync(
                project,
                recordingId,
                firstBinding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None));
        using var secondLease = Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(
            await store.TryAcquireAdmissionLeaseAsync(
                project,
                recordingId,
                secondBinding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None));

        var registrations = await Task.WhenAll(
            firstLease.TryRegisterAsync(
                ResolveStatePath(project, recordingId),
                CreateStored(project, recordingId, firstBinding),
                CancellationToken.None).AsTask(),
            secondLease.TryRegisterAsync(
                ResolveStatePath(project, recordingId),
                CreateStored(project, recordingId, secondBinding),
                CancellationToken.None).AsTask());

        Assert.Single(registrations, static result => result.Registered);
        Assert.Single(registrations, static result => !result.Registered);
        Assert.Equal(
            recordingId,
            (await store.ReadAsync(project, recordingId, CancellationToken.None))!.RecordingId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AdmissionLease_RegistrationIdentityDoesNotMatch_RejectsBeforePersistence ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "game-view-recording-registry",
            "lease-identity");
        var project = CreateProject(scope);
        var store = new FileGameViewRecordingExecutionStore();
        var recordingId = Guid.Parse("9c8e1b23-e969-47fc-a39a-f91cd660b5ac");
        var otherRecordingId = Guid.Parse("5bb8c970-90a9-4e7b-9767-f9ee3846085c");
        var binding = CreateStartBinding(
            Guid.Parse("669bfe68-8b84-4458-a01f-b128f0fdf090"),
            processId: 404);
        var mismatchedBinding = new IpcGameViewRecordingStartBinding(
            new ProcessIdentity(
                binding.Process.ProcessId,
                binding.Process.Generation + 1),
            binding.Runtime,
            binding.Generation);
        using var lease = Assert.IsAssignableFrom<IGameViewRecordingAdmissionLease>(
            await store.TryAcquireAdmissionLeaseAsync(
                project,
                recordingId,
                binding,
                TimeSpan.FromSeconds(5),
                CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() => lease.TryRegisterAsync(
            ResolveStatePath(project, otherRecordingId),
            CreateStored(project, otherRecordingId, binding),
            CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => lease.TryRegisterAsync(
            ResolveStatePath(project, recordingId),
            CreateStored(project, recordingId, mismatchedBinding),
            CancellationToken.None).AsTask());

        Assert.Null(await store.ReadAsync(project, recordingId, CancellationToken.None));
        Assert.Null(await store.ReadAsync(project, otherRecordingId, CancellationToken.None));
    }

    private static ResolvedUnityProjectContext CreateProject (TestDirectoryScope scope) =>
        ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(
            scope,
            ProjectFingerprintTestFactory.Create("game-view-recording-registry"));

    private static IpcGameViewRecordingStartBinding CreateStartBinding (
        Guid runtimeId,
        int processId) =>
        new(
            new ProcessIdentity(processId, Generation: (ulong)processId * 10),
            new GameViewRecordingRuntimeIdentity(
                runtimeId,
                "windows",
                "media-foundation",
                "1"),
            new UnityEditorGenerationSnapshot(
                CompileGeneration: 1,
                DomainReloadGeneration: 1,
                AssetRefreshGeneration: 3,
                PlayModeGeneration: 2));

    private static GameViewRecordingStoredExecution CreateStored (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        IpcGameViewRecordingStartBinding startBinding)
    {
        var effective = CreateEffectiveRequest();
        var requestRef = new PathArtifactRef(
            GameViewRecordingArtifactKinds.Request,
            GameViewRecordingArtifactMediaTypes.Json,
            new ArtifactPath($"artifacts/{recordingId:D}/recording-request.json"),
            effective.Digest,
            sizeBytes: 1,
            ObservedAtUtc);
        var payload = GameViewRecordingPayloadFactory.CreatePreparing(
            project,
            recordingId,
            effective,
            requestRef,
            ObservedAtUtc);
        return new GameViewRecordingStoredExecution(
            GameViewRecordingStoredExecution.CurrentSchemaVersion,
            recordingId,
            new GameViewRecordingRequest(
                effective.SchemaVersion,
                effective.Resolution,
                effective.FrameRate,
                effective.MaxDurationSeconds),
            effective.CanonicalJson,
            effective.Digest,
            requestRef,
            CreateCapability(),
            startBinding,
            ObservedAtUtc.AddSeconds(5),
            runtimeSnapshot: null,
            payload);
    }

    private static GameViewRecordingEffectiveRequest CreateEffectiveRequest ()
    {
        var result = GameViewRecordingRequestNormalizer.Normalize(
            new GameViewRecordingRequestDocument(
                GameViewRecordingRequest.CurrentSchemaVersion,
                new PixelDimensions(1920, 1080),
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
        return Assert.IsType<GameViewRecordingEffectiveRequest>(result.Request);
    }

    private static GameViewRecordingCapability CreateCapability () =>
        new(
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
            new GameViewRecordingCaptureProfile(
                GameViewRecordingContainer.Mp4,
                GameViewRecordingCodec.H264,
                audio: false,
                alpha: false,
                encodingProfile: "h264-main",
                encodingQuality: "high",
                GameViewRecordingTimingMode.ConstantFrameRateCapture));

    private static AbsolutePath ResolveStatePath (
        ResolvedUnityProjectContext project,
        Guid recordingId) =>
        UcliStoragePathResolver.ResolveGameViewRecordingExecutionStatePath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            recordingId);
}
