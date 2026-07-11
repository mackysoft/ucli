using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Screenshot;

public sealed class ScreenshotCaptureServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenGuiSessionIsMissing_FailsBeforePreparingArtifactOrCallingUnity ()
    {
        var artifactStore = new RecordingScreenshotArtifactStore();
        var unityExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(
            DaemonSessionReadResult.Success(session: null),
            unityExecutor,
            artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotRequiresGuiSession, result.Error!.Code);
        Assert.Equal(0, artifactStore.PrepareCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsValidRawImage_CommitsPngAndProjectsCaptureMetadata ()
    {
        var response = CreateResponse(width: 1920, height: 1080);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invocation = Assert.Single(unityExecutor.Invocations);
        Assert.Equal(UcliCommandIds.ScreenshotGame, invocation.Command);
        Assert.Equal(UnityExecutionMode.Daemon, invocation.Mode);
        var payload = Assert.IsType<UnityRequestPayload.ScreenshotCapture>(invocation.Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game), payload.Target);
        Assert.Equal(1920, payload.RequestedWidth);
        Assert.Equal(1080, payload.RequestedHeight);
        Assert.Equal(5000, payload.TimeoutMilliseconds);

        var commit = Assert.Single(artifactStore.CommitRequests);
        Assert.Equal(1920, commit.Width);
        Assert.Equal(1080, commit.Height);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb), commit.PixelFormat);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown), commit.RowOrder);
        Assert.Equal(1920 * 4, commit.RowStrideBytes);
        Assert.Equal((long)1920 * 1080 * 4, commit.SizeBytes);
        Assert.Equal(0, artifactStore.DiscardCount);

        var output = result.Output!;
        Assert.Equal(7, output.DomainReloadGeneration);
        Assert.Equal("playing", output.PlayModeState);
        Assert.Equal(new string('b', 64), output.ArtifactDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsDifferentDimensions_RejectsAndDiscardsStagingWithoutCommit ()
    {
        var response = CreateResponse(width: 1280, height: 720);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
        Assert.Contains("dimensions", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsDimensionsBeyondHostLimit_RejectsBeforeArtifactCommit ()
    {
        var width = IpcScreenshotCaptureLimits.MaximumDimension + 1;
        var response = CreateResponse(width, height: 1, requestedWidth: width, requestedHeight: 1);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(width, height: 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
    }

    [Theory]
    [InlineData("color-space", "linear ")]
    [InlineData("color-space", "unsupported")]
    [InlineData("lifecycle-state", "ready ")]
    [InlineData("lifecycle-state", "unsupported")]
    [InlineData("compile-state", "ready ")]
    [InlineData("compile-state", "unsupported")]
    [InlineData("play-mode-state", "playing ")]
    [InlineData("play-mode-state", "unsupported")]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsNonCanonicalStateMetadata_RejectsAndDiscardsWithoutCommit (
        string caseName,
        string invalidValue)
    {
        var response = caseName switch
        {
            "color-space" => CreateResponse(width: 1920, height: 1080, colorSpace: invalidValue),
            "lifecycle-state" => CreateResponse(width: 1920, height: 1080, lifecycleState: invalidValue),
            "compile-state" => CreateResponse(width: 1920, height: 1080, compileState: invalidValue),
            "play-mode-state" => CreateResponse(width: 1920, height: 1080, playModeState: invalidValue),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown metadata case."),
        };
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityTimesOut_DiscardsPreparedStagingWithoutCommit ()
    {
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(
            new UnityRequestFailure(IpcTransportErrorCodes.IpcTimeout, "Screenshot timed out.")));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenArtifactCommitFails_ReturnsFailureAndDiscardsCaptureLayout ()
    {
        var response = CreateResponse(width: 1920, height: 1080);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var expectedError = ExecutionError.InternalError("PNG commit failed.");
        var artifactStore = new RecordingScreenshotArtifactStore
        {
            CommitResult = ScreenshotArtifactCommitResult.Failure(expectedError),
        };
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
        Assert.Single(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
    }

    private static ScreenshotCaptureService CreateService (
        DaemonSessionReadResult sessionResult,
        IUnityRequestExecutor requestExecutor,
        RecordingScreenshotArtifactStore artifactStore)
    {
        var context = ProjectContextTestFactory.CreateSingleRootProject() with
        {
            Config = ProjectContextTestFactory.CreateSingleRootProject().Config with
            {
                IpcDefaultTimeoutMilliseconds = 5000,
            },
        };
        return new ScreenshotCaptureService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            new RecordingDaemonSessionStore(sessionResult),
            requestExecutor,
            artifactStore,
            new StaticCaptureIdFactory());
    }

    private static ScreenshotCaptureInput CreateInput (
        int width = 1920,
        int height = 1080)
    {
        return new ScreenshotCaptureInput(
            IpcScreenshotTarget.Game,
            ProjectPath: null,
            RequestedWidth: width,
            RequestedHeight: height,
            TimeoutMilliseconds: 5000);
    }

    private static DaemonSessionReadResult CreateGuiSessionResult ()
    {
        return DaemonSessionReadResult.Success(DaemonSessionTestFactory.CreateUserOwned(
            editorMode: "gui",
            endpointAddress: "ucli-screenshot"));
    }

    private static UnityRequestResponse CreateResponse (
        int width,
        int height,
        int requestedWidth = 1920,
        int requestedHeight = 1080,
        string? colorSpace = null,
        string? lifecycleState = null,
        string? compileState = null,
        string? playModeState = null)
    {
        var paths = RecordingScreenshotArtifactStore.Paths;
        var payload = new IpcScreenshotCaptureResponse(
            new IpcScreenshotCapture(
                ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
                ContractLiteralCodec.ToValue(IpcScreenshotSizeMode.RequestedResolution),
                RequestedWidth: requestedWidth,
                RequestedHeight: requestedHeight,
                width,
                height,
                colorSpace ?? ContractLiteralCodec.ToValue(IpcScreenshotColorSpace.Linear),
                lifecycleState ?? IpcEditorLifecycleStateCodec.Ready,
                compileState ?? IpcCompileStateCodec.Ready,
                DomainReloadGeneration: 7,
                playModeState ?? ContractLiteralCodec.ToValue(IpcPlayModeState.Playing)),
            new IpcScreenshotStagingImage(
                paths.RawStagingPath,
                ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
                ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
                RowStrideBytes: width * 4,
                SizeBytes: (long)width * height * 4));
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            IpcProtocol.CurrentVersion,
            "request-1",
            IpcProtocol.StatusOk,
            IpcPayloadCodec.SerializeToElement(payload),
            Errors: []));
    }

    private sealed class StaticCaptureIdFactory : IScreenshotCaptureIdFactory
    {
        public string Create () => "capture-1";
    }

    private sealed class RecordingScreenshotArtifactStore : IScreenshotArtifactStore
    {
        public static ScreenshotArtifactPaths Paths { get; } = new(
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            CaptureId: "capture-1",
            ArtifactDirectory: "/repo/.ucli/local/fingerprints/project-fingerprint/artifacts/screenshot/capture-1",
            PngPath: "/repo/.ucli/local/fingerprints/project-fingerprint/artifacts/screenshot/capture-1/screenshot.png",
            StagingDirectory: "/repo/.ucli/local/fingerprints/project-fingerprint/work/screenshot/capture-1",
            RawStagingPath: "/repo/.ucli/local/fingerprints/project-fingerprint/work/screenshot/capture-1/capture.rgba");

        public int PrepareCount { get; private set; }

        public int DiscardCount { get; private set; }

        public List<ScreenshotArtifactCommitRequest> CommitRequests { get; } = [];

        public ScreenshotArtifactCommitResult? CommitResult { get; init; }

        public ScreenshotArtifactPreparationResult Prepare (
            ResolvedUnityProjectContext unityProject,
            string captureId)
        {
            PrepareCount++;
            return ScreenshotArtifactPreparationResult.Success(Paths);
        }

        public ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
            ScreenshotArtifactCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            CommitRequests.Add(request);
            return ValueTask.FromResult(CommitResult ?? ScreenshotArtifactCommitResult.Success(new ScreenshotArtifact(
                Path: ".ucli/local/fingerprints/project-fingerprint/artifacts/screenshot/capture-1/screenshot.png",
                Digest: new string('b', 64),
                SizeBytes: 1024,
                CreatedAtUtc)));
        }

        public ValueTask<ScreenshotArtifactDiscardResult> DiscardAsync (
            ScreenshotArtifactPaths paths,
            CancellationToken cancellationToken = default)
        {
            DiscardCount++;
            return ValueTask.FromResult(ScreenshotArtifactDiscardResult.Success());
        }
    }
}
