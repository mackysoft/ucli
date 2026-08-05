using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcScreenshotContractSerializationTests
{
    private static readonly Guid CaptureId = Guid.Parse("ab66cdfa-d4bd-49bd-b727-a1201d4426f4");

    [Fact]
    [Trait("Size", "Small")]
    public void ScreenshotContracts_SerializeWithCamelCaseFields ()
    {
        var request = IpcPayloadCodec.SerializeToElement(new IpcScreenshotCaptureRequest(
            CaptureId: CaptureId,
            Target: IpcScreenshotTarget.Game,
            RequestedDimensions: new PixelDimensions(1920, 1080)));
        var response = IpcPayloadCodec.SerializeToElement(new IpcScreenshotCaptureResponse(
            CaptureId: CaptureId,
            Capture: new IpcScreenshotCapture(
                Target: IpcScreenshotTarget.Game,
                SizeMode: IpcScreenshotSizeMode.RequestedResolution,
                RequestedDimensions: new PixelDimensions(1920, 1080),
                Dimensions: new PixelDimensions(1920, 1080),
                ProjectColorSpace: UnityProjectColorSpace.Linear,
                State: new UnityEditorStateSnapshot(
                    editorMode: UnityEditorMode.Gui,
                    lifecycleState: UnityEditorLifecycleState.PlayMode,
                    compileState: UnityEditorCompileState.Ready,
                    generations: new UnityEditorGenerationSnapshot(
                        CompileGeneration: 6,
                        DomainReloadGeneration: 7,
                        AssetRefreshGeneration: 8,
                        PlayModeGeneration: 9),
                    playMode: new UnityEditorPlayModeSnapshot(
                        State: UnityEditorPlayModeState.Playing,
                        Transition: UnityEditorPlayModeTransition.None,
                        IsPlaying: true,
                        IsPlayingOrWillChangePlaymode: true))),
            Staging: new IpcScreenshotStagingImage(
                Dimensions: new PixelDimensions(1920, 1080),
                PixelFormat: IpcScreenshotPixelFormat.Rgba8Srgb,
                RowOrder: IpcScreenshotRowOrder.TopDown,
                RowStrideBytes: 7680,
                SizeBytes: 8294400)));

        JsonAssert.For(request)
            .HasString("captureId", CaptureId.ToString())
            .HasString("target", "game")
            .HasProperty("requestedDimensions", dimensions => dimensions
                .HasInt32("width", 1920)
                .HasInt32("height", 1080));
        Assert.False(request.TryGetProperty("stagingPath", out _));
        Assert.False(request.TryGetProperty("timeoutMilliseconds", out _));
        JsonAssert.For(response)
            .HasString("captureId", CaptureId.ToString())
            .HasProperty("capture", capture => capture
                .HasString("target", "game")
                .HasString("sizeMode", "requestedResolution")
                .HasProperty("requestedDimensions", dimensions => dimensions
                    .HasInt32("width", 1920)
                    .HasInt32("height", 1080))
                .HasProperty("dimensions", dimensions => dimensions
                    .HasInt32("width", 1920)
                    .HasInt32("height", 1080))
                .HasString("projectColorSpace", "linear")
                .HasProperty("state", state => state
                    .HasString("editorMode", "gui")
                    .HasString("lifecycleState", "playmode")
                    .HasString("compileState", "ready")
                    .HasProperty("generations", generations => generations
                        .HasInt32("compileGeneration", 6)
                        .HasInt32("domainReloadGeneration", 7)
                        .HasInt32("assetRefreshGeneration", 8)
                        .HasInt32("playModeGeneration", 9))
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "playing"))))
            .HasProperty("staging", staging => staging
                .HasProperty("dimensions", dimensions => dimensions
                    .HasInt32("width", 1920)
                    .HasInt32("height", 1080))
                .HasString("pixelFormat", "rgba8Srgb")
                .HasString("rowOrder", "topDown")
                .HasInt32("rowStrideBytes", 7680)
                .HasInt32("sizeBytes", 8294400));
        Assert.False(response.GetProperty("staging").TryGetProperty("path", out _));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("request", "target")]
    [InlineData("capture", "target")]
    [InlineData("capture", "sizeMode")]
    [InlineData("capture", "projectColorSpace")]
    [InlineData("staging", "pixelFormat")]
    [InlineData("staging", "rowOrder")]
    public void ScreenshotContracts_WhenRequiredEnumIsMissing_RejectJson (
        string contractName,
        string propertyName)
    {
        var (contract, contractType) = CreateContract(contractName);
        var json = JsonSerializer.SerializeToNode(
            contract,
            contractType,
            IpcJsonSerializerOptions.Default)!.AsObject();
        Assert.True(json.Remove(propertyName));

        var exception = Record.Exception(() =>
            JsonSerializer.Deserialize(json, contractType, IpcJsonSerializerOptions.Default));

        Assert.NotNull(exception);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("request-target")]
    [InlineData("capture-target")]
    [InlineData("capture-size-mode")]
    [InlineData("capture-project-color-space")]
    [InlineData("staging-pixel-format")]
    [InlineData("staging-row-order")]
    public void ScreenshotContracts_WhenFiniteLiteralIsUndefined_ThrowArgumentOutOfRangeException (string caseName)
    {
        Action construction = caseName switch
        {
            "request-target" => () => _ = new IpcScreenshotCaptureRequest(
                CaptureId,
                (IpcScreenshotTarget)0,
                null),
            "capture-target" => () => _ = new IpcScreenshotCapture(
                (IpcScreenshotTarget)0,
                IpcScreenshotSizeMode.CurrentSurface,
                null,
                new PixelDimensions(1, 1),
                UnityProjectColorSpace.Linear,
                CreateStableEditState()),
            "capture-size-mode" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                (IpcScreenshotSizeMode)0,
                null,
                new PixelDimensions(1, 1),
                UnityProjectColorSpace.Linear,
                CreateStableEditState()),
            "capture-project-color-space" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.CurrentSurface,
                null,
                new PixelDimensions(1, 1),
                (UnityProjectColorSpace)0,
                CreateStableEditState()),
            "staging-pixel-format" => () => _ = new IpcScreenshotStagingImage(
                new PixelDimensions(1, 1),
                (IpcScreenshotPixelFormat)0,
                IpcScreenshotRowOrder.TopDown,
                4,
                4),
            "staging-row-order" => () => _ = new IpcScreenshotStagingImage(
                new PixelDimensions(1, 1),
                IpcScreenshotPixelFormat.Rgba8Srgb,
                (IpcScreenshotRowOrder)0,
                4,
                4),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown case name."),
        };

        Assert.Throws<ArgumentOutOfRangeException>(construction);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("request-empty-id")]
    [InlineData("request-layout")]
    [InlineData("request-scene-size")]
    [InlineData("capture-size-mode")]
    [InlineData("capture-dimensions")]
    [InlineData("staging-row-stride")]
    [InlineData("staging-size")]
    [InlineData("response-empty-id")]
    [InlineData("response-dimensions")]
    public void ScreenshotContracts_WhenAggregateInvariantIsViolated_ThrowArgumentException (string caseName)
    {
        var capture = CreateCapture();
        var staging = CreateStaging();
        Action construction = caseName switch
        {
            "request-empty-id" => () => _ = new IpcScreenshotCaptureRequest(
                Guid.Empty,
                IpcScreenshotTarget.Game,
                null),
            "request-layout" => () => _ = new IpcScreenshotCaptureRequest(
                CaptureId,
                IpcScreenshotTarget.Game,
                new PixelDimensions(int.MaxValue, int.MaxValue)),
            "request-scene-size" => () => _ = new IpcScreenshotCaptureRequest(
                CaptureId,
                IpcScreenshotTarget.Scene,
                new PixelDimensions(1, 1)),
            "capture-size-mode" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.CurrentSurface,
                new PixelDimensions(1, 1),
                new PixelDimensions(1, 1),
                UnityProjectColorSpace.Linear,
                CreateStableEditState()),
            "capture-dimensions" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.RequestedResolution,
                new PixelDimensions(1, 1),
                new PixelDimensions(2, 1),
                UnityProjectColorSpace.Linear,
                CreateStableEditState()),
            "staging-row-stride" => () => _ = new IpcScreenshotStagingImage(
                new PixelDimensions(1, 1),
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                3,
                4),
            "staging-size" => () => _ = new IpcScreenshotStagingImage(
                new PixelDimensions(1, 1),
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                4,
                5),
            "response-empty-id" => () => _ = new IpcScreenshotCaptureResponse(Guid.Empty, capture, staging),
            "response-dimensions" => () => _ = new IpcScreenshotCaptureResponse(
                CaptureId,
                capture,
                new IpcScreenshotStagingImage(
                    new PixelDimensions(2, 1),
                    IpcScreenshotPixelFormat.Rgba8Srgb,
                    IpcScreenshotRowOrder.TopDown,
                    8,
                    8)),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown case name."),
        };

        Assert.ThrowsAny<ArgumentException>(construction);
    }

    [Theory]
    [InlineData(UnityEditorLifecycleState.Ready, UnityEditorPlayModeState.Stopped, false, false)]
    [InlineData(UnityEditorLifecycleState.PlayMode, UnityEditorPlayModeState.Playing, true, true)]
    [Trait("Size", "Small")]
    public void ScreenshotCapture_WithSupportedStableState_Constructs (
        UnityEditorLifecycleState lifecycleState,
        UnityEditorPlayModeState playModeState,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode)
    {
        var capture = new IpcScreenshotCapture(
            IpcScreenshotTarget.Game,
            IpcScreenshotSizeMode.CurrentSurface,
            RequestedDimensions: null,
            Dimensions: new PixelDimensions(1, 1),
            UnityProjectColorSpace.Linear,
            CreateState(
                lifecycleState,
                UnityEditorCompileState.Ready,
                playModeState,
                UnityEditorPlayModeTransition.None,
                isPlaying,
                isPlayingOrWillChangePlaymode));

        Assert.Equal(lifecycleState, capture.State.LifecycleState);
    }

    [Theory]
    [InlineData(IpcScreenshotTarget.Game, UnityEditorLifecycleState.Ready, UnityEditorCompileState.Ready, UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true)]
    [InlineData(IpcScreenshotTarget.Game, UnityEditorLifecycleState.PlayMode, UnityEditorCompileState.Compiling, UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, true)]
    [InlineData(IpcScreenshotTarget.Game, UnityEditorLifecycleState.PlayMode, UnityEditorCompileState.Ready, UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.Entering, true, true)]
    [InlineData(IpcScreenshotTarget.Game, UnityEditorLifecycleState.PlayMode, UnityEditorCompileState.Ready, UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, false, true)]
    [InlineData(IpcScreenshotTarget.Game, UnityEditorLifecycleState.PlayMode, UnityEditorCompileState.Ready, UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None, true, false)]
    [InlineData(IpcScreenshotTarget.Game, UnityEditorLifecycleState.Ready, UnityEditorCompileState.Ready, UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None, true, false)]
    [InlineData(IpcScreenshotTarget.Game, UnityEditorLifecycleState.Ready, UnityEditorCompileState.Ready, UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None, false, true)]
    [Trait("Size", "Small")]
    public void ScreenshotCapture_WithUnsupportedOrIncoherentState_ThrowsArgumentException (
        IpcScreenshotTarget target,
        UnityEditorLifecycleState lifecycleState,
        UnityEditorCompileState compileState,
        UnityEditorPlayModeState playModeState,
        UnityEditorPlayModeTransition playModeTransition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode)
    {
        Assert.Throws<ArgumentException>(() => new IpcScreenshotCapture(
            target,
            IpcScreenshotSizeMode.CurrentSurface,
            RequestedDimensions: null,
            Dimensions: new PixelDimensions(1, 1),
            UnityProjectColorSpace.Linear,
            CreateState(
                lifecycleState,
                compileState,
                playModeState,
                playModeTransition,
                isPlaying,
                isPlayingOrWillChangePlaymode)));
    }

    private static (object Contract, Type ContractType) CreateContract (string contractName)
    {
        object contract = contractName switch
        {
            "request" => new IpcScreenshotCaptureRequest(
                CaptureId,
                IpcScreenshotTarget.Game,
                null),
            "capture" => CreateCapture(),
            "staging" => CreateStaging(),
            _ => throw new ArgumentOutOfRangeException(nameof(contractName), contractName, "Unknown contract name."),
        };
        return (contract, contract.GetType());
    }

    private static IpcScreenshotCapture CreateCapture ()
    {
        return new IpcScreenshotCapture(
            IpcScreenshotTarget.Game,
            IpcScreenshotSizeMode.CurrentSurface,
            null,
            new PixelDimensions(1, 1),
            UnityProjectColorSpace.Linear,
            CreateStableEditState());
    }

    private static IpcScreenshotStagingImage CreateStaging ()
    {
        return new IpcScreenshotStagingImage(
            new PixelDimensions(1, 1),
            IpcScreenshotPixelFormat.Rgba8Srgb,
            IpcScreenshotRowOrder.TopDown,
            4,
            4);
    }

    private static UnityEditorStateSnapshot CreateStableEditState ()
    {
        return CreateState(
            UnityEditorLifecycleState.Ready,
            UnityEditorCompileState.Ready,
            UnityEditorPlayModeState.Stopped,
            UnityEditorPlayModeTransition.None,
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false);
    }

    private static UnityEditorStateSnapshot CreateState (
        UnityEditorLifecycleState lifecycleState,
        UnityEditorCompileState compileState,
        UnityEditorPlayModeState playModeState,
        UnityEditorPlayModeTransition playModeTransition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode)
    {
        return new UnityEditorStateSnapshot(
            UnityEditorMode.Gui,
            lifecycleState,
            compileState,
            new UnityEditorGenerationSnapshot(1, 2, 3, 4),
            new UnityEditorPlayModeSnapshot(
                playModeState,
                playModeTransition,
                isPlaying,
                isPlayingOrWillChangePlaymode));
    }
}
