using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Screenshot;

public sealed class ScreenshotCaptureServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid CaptureId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

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
        Assert.Equal(CaptureId, payload.Request.CaptureId);
        Assert.Equal(target, payload.Request.Target);
        Assert.Equal(requestedWidth, payload.Request.RequestedWidth);
        Assert.Equal(requestedHeight, payload.Request.RequestedHeight);
        Assert.Equal(TimeSpan.FromMilliseconds(5000), invocation.Timeout);
        Assert.Equal(CaptureId, Assert.Single(artifactStore.CaptureIds));

        var commit = Assert.Single(artifactStore.CommitRequests);
        Assert.Equal(width, commit.Width);
        Assert.Equal(height, commit.Height);
        Assert.Equal(IpcScreenshotPixelFormat.Rgba8Srgb, commit.PixelFormat);
        Assert.Equal(IpcScreenshotRowOrder.TopDown, commit.RowOrder);
        Assert.Equal(width * 4, commit.RowStrideBytes);
        Assert.Equal((long)width * height * 4, commit.SizeBytes);
        Assert.Equal(1, artifactStore.DiscardCount);

        var output = result.Output!;
        Assert.Equal(target, output.Capture.Target);
        Assert.Equal(7, output.Capture.State.Generations.DomainReloadGeneration);
        Assert.Equal(IpcPlayModeState.Stopped, output.Capture.State.PlayMode.State);
        Assert.Equal(Sha256Digest.Parse(new string('b', 64)), output.Artifact.Digest);
    }

    [Theory]
    [InlineData(IpcScreenshotTarget.Game, 1920, 1080, 1920, 1080)]
    [InlineData(IpcScreenshotTarget.Scene, 1280, 720, null, null)]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsStablePlayModeState_CommitsPng (
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
            target: target,
            state: CreateState(
                lifecycleState: IpcEditorLifecycleState.PlayMode,
                playModeState: IpcPlayModeState.Playing,
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(
            CreateGuiSessionResult(),
            new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response)),
            artifactStore);

        var result = await service.CaptureAsync(
            CreateInput(requestedWidth, requestedHeight, target: target),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(artifactStore.CommitRequests);
        Assert.Equal(target, result.Output!.Capture.Target);
        Assert.Equal(IpcEditorLifecycleState.PlayMode, result.Output.Capture.State.LifecycleState);
        Assert.Equal(IpcPlayModeState.Playing, result.Output.Capture.State.PlayMode.State);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsIncoherentCaptureState_RejectsAndDiscardsWithoutCommit ()
    {
        var response = CreateResponse(
            width: 1920,
            height: 1080,
            playModeState: ContractLiteralCodec.ToValue(IpcPlayModeState.Playing));
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
        Assert.Contains("payload is invalid", result.Error!.Message, StringComparison.Ordinal);
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
        Assert.Equal(TimeSpan.FromMilliseconds(7000), invocation.Timeout);
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
    public async Task Capture_WhenUnityReturnsDifferentCaptureId_RejectsAndDiscardsStagingWithoutCommit ()
    {
        var response = CreateResponse(
            width: 1920,
            height: 1080,
            captureId: Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(artifactStore.CommitRequests);
        Assert.Equal(1, artifactStore.DiscardCount);
        Assert.Contains("identifier", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Capture_WhenUnityReturnsMalformedStagingLayout_RejectsBeforeCommit ()
    {
        var response = CreateMalformedResponse(
            width: 1920,
            height: 1080,
            requestedWidth: 1920,
            requestedHeight: 1080,
            target: IpcScreenshotTarget.Game,
            colorSpace: null,
            lifecycleState: null,
            compileState: null,
            playModeState: null,
            stagingSizeBytes: 1);
        var unityExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(response));
        var artifactStore = new RecordingScreenshotArtifactStore();
        var service = CreateService(CreateGuiSessionResult(), unityExecutor, artifactStore);

        var result = await service.CaptureAsync(CreateInput(), CancellationToken.None);

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
        int? screenshotTimeoutMilliseconds = null,
        Guid? captureId = null)
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
            new StaticGuidGenerator(captureId ?? CaptureId));
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
        Guid? captureId = null,
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
                stagingSizeBytes: null);
        }

        var payload = new IpcScreenshotCaptureResponse(
            captureId ?? CaptureId,
            new IpcScreenshotCapture(
                target,
                requestedWidth.HasValue
                    ? IpcScreenshotSizeMode.RequestedResolution
                    : IpcScreenshotSizeMode.CurrentSurface,
                RequestedWidth: requestedWidth,
                RequestedHeight: requestedHeight,
                Width: width,
                Height: height,
                IpcScreenshotColorSpace.Linear,
                State: state ?? CreateState()),
            new IpcScreenshotStagingImage(
                width,
                height,
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                RowStrideBytes: width * 4,
                SizeBytes: (long)width * height * 4));
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            IpcResponseStatus.Ok,
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
        long? stagingSizeBytes)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            captureId = CaptureId,
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
                width,
                height,
                pixelFormat = ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
                rowOrder = ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
                rowStrideBytes = width * 4,
                sizeBytes = stagingSizeBytes ?? ((long)width * height * 4),
            },
        });
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            IpcResponseStatus.Ok,
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

    private sealed class RecordingScreenshotArtifactStore : IScreenshotArtifactStore
    {
        public int PrepareCount { get; private set; }

        public int DiscardCount { get; private set; }

        public List<IpcScreenshotStagingImage> CommitRequests { get; } = [];

        public List<Guid> CaptureIds { get; } = [];

        public ScreenshotArtifactCommitResult? CommitResult { get; init; }

        public ScreenshotArtifactDiscardResult? DiscardResult { get; init; }

        public ScreenshotArtifactPreparationResult Prepare (
            ResolvedUnityProjectContext unityProject,
            Guid captureId)
        {
            PrepareCount++;
            CaptureIds.Add(captureId);
            return ScreenshotArtifactPreparationResult.Success(new RecordingScreenshotArtifactLease(this));
        }

        private sealed class RecordingScreenshotArtifactLease : IScreenshotArtifactLease
        {
            private readonly RecordingScreenshotArtifactStore store;

            public RecordingScreenshotArtifactLease (RecordingScreenshotArtifactStore store)
            {
                this.store = store;
            }

            public ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
                IpcScreenshotStagingImage staging,
                CancellationToken cancellationToken = default)
            {
                store.CommitRequests.Add(staging);
                return ValueTask.FromResult(store.CommitResult ?? ScreenshotArtifactCommitResult.Success(new ScreenshotArtifact(
                    path: ".ucli/local/projects/<projectStorageKey>/artifacts/screenshot/<captureStorageKey>/screenshot.png",
                    digest: Sha256Digest.Parse(new string('b', 64)),
                    sizeBytes: 1024,
                    createdAtUtc: CreatedAtUtc)));
            }

            public ScreenshotArtifactDiscardResult Discard ()
            {
                store.DiscardCount++;
                return store.DiscardResult ?? ScreenshotArtifactDiscardResult.Success();
            }
        }
    }
}
