using System.Text.Json;
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
            DaemonSessionReadResult.Missing(),
            unityExecutor,
            artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotRequiresGuiSession, result.Error!.Code);
        Assert.Equal(0, artifactStore.PrepareCount);
    }

    [Theory]
    [InlineData(IpcScreenshotTarget.Game, 1920, 1080, 1920, 1080)]
    [InlineData(IpcScreenshotTarget.Scene, 1280, 720, null, null)]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsValidRawImage_UsesScreenshotIdentityAndCommitsPng (
        IpcScreenshotTarget target,
        int width,
        int height,
        int? requestedWidth,
        int? requestedHeight)
    {
        var response = CreateResponse(
            width,
            height,
            requestedWidth,
            requestedHeight,
            target: target);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(
            CreateInput(requestedWidth, requestedHeight, target: target),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invocation = Assert.Single(unityExecutor.Invocations);
        Assert.Equal(UcliCommandIds.Screenshot, invocation.Command);
        Assert.Equal(UnityExecutionMode.Daemon, invocation.Mode);
        var payload = Assert.IsType<UnityRequestPayload.ScreenshotCapture>(invocation.Payload);
        Assert.Equal(target, payload.Target);
        Assert.Equal(requestedWidth, payload.RequestedWidth);
        Assert.Equal(requestedHeight, payload.RequestedHeight);
        Assert.Equal(RecordingScreenshotArtifactStore.RawStagingPath, payload.StagingPath);
        Assert.Equal(5000, payload.TimeoutMilliseconds);

        var commit = Assert.Single(artifactStore.CommitRequests);
        Assert.Equal(width, commit.Width);
        Assert.Equal(height, commit.Height);
        Assert.Equal(IpcScreenshotPixelFormat.Rgba8Srgb, commit.Staging.PixelFormat);
        Assert.Equal(IpcScreenshotRowOrder.TopDown, commit.Staging.RowOrder);
        Assert.Equal(width * 4, commit.Staging.RowStrideBytes);
        Assert.Equal((long)width * height * 4, commit.Staging.SizeBytes);
        Assert.Equal(1, artifactStore.DiscardCount);

        var output = result.Output!;
        Assert.Equal(target, output.Capture.Target);
        Assert.Equal(7, output.Capture.State.Generations.DomainReloadGeneration);
        Assert.Equal(IpcPlayModeState.Stopped, output.Capture.State.PlayMode.State);
        Assert.Equal(new string('b', 64), output.Artifact.Digest);
    }

    [Theory]
    [InlineData(
        IpcEditorLifecycleState.PlayMode,
        IpcCompileState.Ready,
        IpcPlayModeState.Stopped,
        IpcPlayModeTransition.None,
        false,
        false)]
    [InlineData(
        IpcEditorLifecycleState.Ready,
        IpcCompileState.Compiling,
        IpcPlayModeState.Stopped,
        IpcPlayModeTransition.None,
        false,
        false)]
    [InlineData(
        IpcEditorLifecycleState.Ready,
        IpcCompileState.Ready,
        IpcPlayModeState.Playing,
        IpcPlayModeTransition.None,
        false,
        false)]
    [InlineData(
        IpcEditorLifecycleState.Ready,
        IpcCompileState.Ready,
        IpcPlayModeState.Stopped,
        IpcPlayModeTransition.Entering,
        false,
        false)]
    [InlineData(
        IpcEditorLifecycleState.Ready,
        IpcCompileState.Ready,
        IpcPlayModeState.Stopped,
        IpcPlayModeTransition.None,
        true,
        false)]
    [InlineData(
        IpcEditorLifecycleState.Ready,
        IpcCompileState.Ready,
        IpcPlayModeState.Stopped,
        IpcPlayModeTransition.None,
        false,
        true)]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsNonCaptureReadyState_RejectsAndDiscardsWithoutCommit (
        IpcEditorLifecycleState lifecycleState,
        IpcCompileState compileState,
        IpcPlayModeState playModeState,
        IpcPlayModeTransition playModeTransition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode)
    {
        var response = CreateResponse(
            width: 1920,
            height: 1080,
            state: CreateState(
                lifecycleState,
                compileState,
                playModeState,
                playModeTransition,
                isPlaying,
                isPlayingOrWillChangePlaymode));
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
        Assert.Contains("capture state", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenScreenshotTimeoutOverrideExists_UsesRootCommandOverride ()
    {
        var response = CreateResponse(width: 1920, height: 1080);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(
            CreateGuiSessionResult(),
            unityExecutor,
            artifactStore,
            screenshotTimeoutMilliseconds: 7000);

        var result = await service.CaptureAsync(
            CreateInput(timeoutMilliseconds: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invocation = Assert.Single(unityExecutor.Invocations);
        Assert.Equal(UcliCommandIds.Screenshot, invocation.Command);
        var payload = Assert.IsType<UnityRequestPayload.ScreenshotCapture>(invocation.Payload);
        Assert.Equal(7000, payload.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenTargetIsUndefined_RejectsBeforePreparingArtifactOrCallingUnity ()
    {
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(
            CreateGuiSessionResult(),
            new UnexpectedUnityRequestExecutor(),
            artifactStore);

        var result = await service.CaptureAsync(
            CreateInput(target: (IpcScreenshotTarget)int.MaxValue),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("target", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, artifactStore.PrepareCount);
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

    [Theory]
    [InlineData("dimensions")]
    [InlineData("returned-path")]
    [Trait("Size", "Small")]
    public async Task Capture_WhenRawStagingContractIsUnsupported_DelegatesValidationToArtifactStore (string caseName)
    {
        var width = caseName == "dimensions"
            ? IpcScreenshotCaptureLimits.MaximumDimension + 1
            : 1920;
        var height = caseName == "dimensions" ? 1 : 1080;
        var returnedStagingPath = caseName == "returned-path"
            ? RecordingScreenshotArtifactStore.RawStagingPath + ".unexpected"
            : null;
        var response = CreateResponse(
            width,
            height,
            requestedWidth: width,
            requestedHeight: height,
            stagingPath: returnedStagingPath);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore
        {
            CommitResult = ScreenshotArtifactCommitResult.Failure(ExecutionError.InternalError(
                "Screenshot staging contract is unsupported.",
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported)),
        };
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(width, height), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotCaptureUnsupported, result.Error!.Code);
        var commit = Assert.Single(artifactStore.CommitRequests);
        Assert.Equal(width, commit.Width);
        Assert.Equal(returnedStagingPath ?? RecordingScreenshotArtifactStore.RawStagingPath, commit.Staging.Path);
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
            new UnityRequestFailure(
                UnityRequestFailureKind.General,
                IpcTransportErrorCodes.IpcTimeout,
                "Screenshot timed out.")));
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenArtifactCommitSucceedsButLeaseTerminationFails_ReturnsCleanupFailure ()
    {
        var response = CreateResponse(width: 1920, height: 1080);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var cleanupError = ExecutionError.InternalError("Screenshot staging cleanup failed.");
        var artifactStore = new RecordingScreenshotArtifactStore
        {
            DiscardResult = ScreenshotArtifactDiscardResult.Failure(cleanupError),
        };
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(cleanupError, result.Error);
        Assert.Single(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenExecutionIsCanceledAndDiscardFails_ReturnsObservableCleanupFailure ()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var unityExecutor = new RecordingUnityRequestExecutor(_ =>
        {
            cancellationTokenSource.Cancel();
            throw new OperationCanceledException("Screenshot capture was canceled.", cancellationTokenSource.Token);
        });
        var cleanupError = ExecutionError.InternalError("Screenshot staging cleanup failed.");
        var artifactStore = new RecordingScreenshotArtifactStore
        {
            DiscardResult = ScreenshotArtifactDiscardResult.Failure(cleanupError),
        };
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), cancellationTokenSource.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("capture was interrupted", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(cleanupError.Message, result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1, artifactStore.DiscardCount);
    }

    private static ScreenshotCaptureService CreateService (
        DaemonSessionReadResult sessionResult,
        IUnityRequestExecutor requestExecutor,
        RecordingScreenshotArtifactStore artifactStore,
        int? screenshotTimeoutMilliseconds = null)
    {
        var context = ProjectContextTestFactory.CreateSingleRootProject() with
        {
            Config = ProjectContextTestFactory.CreateSingleRootProject().Config with
            {
                IpcDefaultTimeoutMilliseconds = 5000,
                IpcTimeoutMillisecondsByCommand = screenshotTimeoutMilliseconds.HasValue
                    ? new Dictionary<string, int?>(StringComparer.Ordinal)
                    {
                        [UcliCommandIds.Screenshot.Name] = screenshotTimeoutMilliseconds.Value,
                    }
                    : new Dictionary<string, int?>(StringComparer.Ordinal),
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
        int? width = 1920,
        int? height = 1080,
        int? timeoutMilliseconds = 5000,
        IpcScreenshotTarget target = IpcScreenshotTarget.Game)
    {
        return new ScreenshotCaptureInput(
            target,
            ProjectPath: null,
            RequestedWidth: width,
            RequestedHeight: height,
            TimeoutMilliseconds: timeoutMilliseconds);
    }

    private static DaemonSessionReadResult CreateGuiSessionResult ()
    {
        return DaemonSessionReadResultTestFactory.Found(DaemonSessionTestFactory.CreateUserOwned(
            editorMode: DaemonEditorMode.Gui,
            endpointAddress: "ucli-screenshot",
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId));
    }

    private static UnityRequestResponse CreateResponse (
        int width,
        int height,
        int? requestedWidth = 1920,
        int? requestedHeight = 1080,
        string? colorSpace = null,
        string? lifecycleState = null,
        string? compileState = null,
        string? playModeState = null,
        IpcScreenshotTarget target = IpcScreenshotTarget.Game,
        string? stagingPath = null,
        UnityEditorStateSnapshot? state = null)
    {
        if (colorSpace is not null
            || lifecycleState is not null
            || compileState is not null
            || playModeState is not null)
        {
            return CreateMalformedResponse(
                width,
                height,
                requestedWidth,
                requestedHeight,
                target,
                colorSpace,
                lifecycleState,
                compileState,
                playModeState,
                stagingPath ?? RecordingScreenshotArtifactStore.RawStagingPath);
        }

        var payload = new IpcScreenshotCaptureResponse(
            new IpcScreenshotCapture(
                target,
                requestedWidth.HasValue
                    ? IpcScreenshotSizeMode.RequestedResolution
                    : IpcScreenshotSizeMode.CurrentSurface,
                requestedWidth: requestedWidth,
                requestedHeight: requestedHeight,
                width,
                height,
                IpcScreenshotColorSpace.Linear,
                state: state ?? CreateState()),
            new IpcScreenshotStagingImage(
                stagingPath ?? RecordingScreenshotArtifactStore.RawStagingPath,
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                RowStrideBytes: width * 4,
                SizeBytes: (long)width * height * 4));
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            IpcProtocol.StatusOk,
            IpcPayloadCodec.SerializeToElement(payload),
            errors: []));
    }

    private static UnityRequestResponse CreateMalformedResponse (
        int width,
        int height,
        int? requestedWidth,
        int? requestedHeight,
        IpcScreenshotTarget target,
        string? colorSpace,
        string? lifecycleState,
        string? compileState,
        string? playModeState,
        string stagingPath)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            capture = new
            {
                target = ContractLiteralCodec.ToValue(target),
                sizeMode = ContractLiteralCodec.ToValue(requestedWidth.HasValue
                    ? IpcScreenshotSizeMode.RequestedResolution
                    : IpcScreenshotSizeMode.CurrentSurface),
                requestedWidth,
                requestedHeight,
                width,
                height,
                colorSpace = colorSpace ?? ContractLiteralCodec.ToValue(IpcScreenshotColorSpace.Linear),
                state = new
                {
                    editorMode = ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                    lifecycleState = lifecycleState ?? ContractLiteralCodec.ToValue(IpcEditorLifecycleState.Ready),
                    compileState = compileState ?? ContractLiteralCodec.ToValue(IpcCompileState.Ready),
                    generations = new
                    {
                        compileGeneration = 5,
                        domainReloadGeneration = 7,
                        assetRefreshGeneration = 8,
                        playModeGeneration = 9,
                    },
                    playMode = new
                    {
                        state = playModeState ?? ContractLiteralCodec.ToValue(IpcPlayModeState.Stopped),
                        transition = ContractLiteralCodec.ToValue(IpcPlayModeTransition.None),
                        isPlaying = false,
                        isPlayingOrWillChangePlaymode = false,
                    },
                },
            },
            staging = new
            {
                path = stagingPath,
                pixelFormat = ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
                rowOrder = ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
                rowStrideBytes = width * 4,
                sizeBytes = (long)width * height * 4,
            },
        });
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            IpcProtocol.StatusOk,
            payload,
            errors: []));
    }

    private static UnityEditorStateSnapshot CreateState (
        IpcEditorLifecycleState lifecycleState = IpcEditorLifecycleState.Ready,
        IpcCompileState compileState = IpcCompileState.Ready,
        IpcPlayModeState playModeState = IpcPlayModeState.Stopped,
        IpcPlayModeTransition playModeTransition = IpcPlayModeTransition.None,
        bool isPlaying = false,
        bool isPlayingOrWillChangePlaymode = false)
    {
        return new UnityEditorStateSnapshot(
            editorMode: DaemonEditorMode.Gui,
            lifecycleState,
            compileState,
            generations: new IpcUnityGenerationSnapshot(
                CompileGeneration: 5,
                DomainReloadGeneration: 7,
                AssetRefreshGeneration: 8,
                PlayModeGeneration: 9),
            playMode: new IpcPlayModeSnapshot(
                State: playModeState,
                Transition: playModeTransition,
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode));
    }

    private sealed class StaticCaptureIdFactory : IScreenshotCaptureIdFactory
    {
        public string Create () => "capture-1";
    }

    private sealed class RecordingScreenshotArtifactStore : IScreenshotArtifactStore
    {
        public const string RawStagingPath =
            "/repo/.ucli/local/fingerprints/project-fingerprint/work/screenshot/capture-1/capture.rgba";

        public int PrepareCount { get; private set; }

        public int DiscardCount { get; private set; }

        public List<ScreenshotArtifactCommitRequest> CommitRequests { get; } = [];

        public ScreenshotArtifactCommitResult? CommitResult { get; init; }

        public ScreenshotArtifactDiscardResult? DiscardResult { get; init; }

        public ScreenshotArtifactPreparationResult Prepare (
            ResolvedUnityProjectContext unityProject,
            string captureId)
        {
            PrepareCount++;
            return ScreenshotArtifactPreparationResult.Success(new RecordingScreenshotArtifactLease(this));
        }

        private sealed class RecordingScreenshotArtifactLease : IScreenshotArtifactLease
        {
            private readonly RecordingScreenshotArtifactStore store;

            public RecordingScreenshotArtifactLease (RecordingScreenshotArtifactStore store)
            {
                this.store = store;
            }

            public string RawStagingPath => RecordingScreenshotArtifactStore.RawStagingPath;

            public ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
                ScreenshotArtifactCommitRequest request,
                CancellationToken cancellationToken = default)
            {
                store.CommitRequests.Add(request);
                return ValueTask.FromResult(store.CommitResult ?? ScreenshotArtifactCommitResult.Success(new ScreenshotArtifact(
                    Path: ".ucli/local/fingerprints/project-fingerprint/artifacts/screenshot/capture-1/screenshot.png",
                    Digest: new string('b', 64),
                    SizeBytes: 1024,
                    CreatedAtUtc)));
            }

            public ScreenshotArtifactDiscardResult Discard ()
            {
                store.DiscardCount++;
                return store.DiscardResult ?? ScreenshotArtifactDiscardResult.Success();
            }
        }
    }
}
