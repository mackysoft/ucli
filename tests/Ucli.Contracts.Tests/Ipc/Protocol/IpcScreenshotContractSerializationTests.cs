using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
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
            RequestedWidth: 1920,
            RequestedHeight: 1080));
        var response = IpcPayloadCodec.SerializeToElement(new IpcScreenshotCaptureResponse(
            CaptureId: CaptureId,
            Capture: new IpcScreenshotCapture(
                Target: IpcScreenshotTarget.Game,
                SizeMode: IpcScreenshotSizeMode.RequestedResolution,
                RequestedWidth: 1920,
                RequestedHeight: 1080,
                Width: 1920,
                Height: 1080,
                ColorSpace: IpcScreenshotColorSpace.Linear,
                State: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Gui,
                    lifecycleState: IpcEditorLifecycleState.Ready,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(
                        CompileGeneration: 6,
                        DomainReloadGeneration: 7,
                        AssetRefreshGeneration: 8,
                        PlayModeGeneration: 9),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Playing,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: true,
                        IsPlayingOrWillChangePlaymode: true))),
            Staging: new IpcScreenshotStagingImage(
                Width: 1920,
                Height: 1080,
                PixelFormat: IpcScreenshotPixelFormat.Rgba8Srgb,
                RowOrder: IpcScreenshotRowOrder.TopDown,
                RowStrideBytes: 7680,
                SizeBytes: 8294400)));

        JsonAssert.For(request)
            .HasString("captureId", CaptureId.ToString())
            .HasString("target", "game")
            .HasInt32("requestedWidth", 1920)
            .HasInt32("requestedHeight", 1080);
        Assert.False(request.TryGetProperty("stagingPath", out _));
        Assert.False(request.TryGetProperty("timeoutMilliseconds", out _));
        JsonAssert.For(response)
            .HasString("captureId", CaptureId.ToString())
            .HasProperty("capture", capture => capture
                .HasString("target", "game")
                .HasString("sizeMode", "requestedResolution")
                .HasInt32("width", 1920)
                .HasInt32("height", 1080)
                .HasString("colorSpace", "linear")
                .HasProperty("state", state => state
                    .HasString("editorMode", "gui")
                    .HasString("lifecycleState", "ready")
                    .HasString("compileState", "ready")
                    .HasProperty("generations", generations => generations
                        .HasInt32("compileGeneration", 6)
                        .HasInt32("domainReloadGeneration", 7)
                        .HasInt32("assetRefreshGeneration", 8)
                        .HasInt32("playModeGeneration", 9))
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "playing"))))
            .HasProperty("staging", staging => staging
                .HasInt32("width", 1920)
                .HasInt32("height", 1080)
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
    [InlineData("capture", "colorSpace")]
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
    [InlineData("capture-color-space")]
    [InlineData("staging-pixel-format")]
    [InlineData("staging-row-order")]
    public void ScreenshotContracts_WhenFiniteLiteralIsUndefined_ThrowArgumentOutOfRangeException (string caseName)
    {
        Action construction = caseName switch
        {
            "request-target" => () => _ = new IpcScreenshotCaptureRequest(
                CaptureId,
                (IpcScreenshotTarget)0,
                null,
                null),
            "capture-target" => () => _ = new IpcScreenshotCapture(
                (IpcScreenshotTarget)0,
                IpcScreenshotSizeMode.CurrentSurface,
                null,
                null,
                1,
                1,
                IpcScreenshotColorSpace.Linear,
                CreateState()),
            "capture-size-mode" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                (IpcScreenshotSizeMode)0,
                null,
                null,
                1,
                1,
                IpcScreenshotColorSpace.Linear,
                CreateState()),
            "capture-color-space" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.CurrentSurface,
                null,
                null,
                1,
                1,
                (IpcScreenshotColorSpace)0,
                CreateState()),
            "staging-pixel-format" => () => _ = new IpcScreenshotStagingImage(
                1,
                1,
                (IpcScreenshotPixelFormat)0,
                IpcScreenshotRowOrder.TopDown,
                4,
                4),
            "staging-row-order" => () => _ = new IpcScreenshotStagingImage(
                1,
                1,
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
    [InlineData("request-unpaired-size")]
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
                null,
                null),
            "request-unpaired-size" => () => _ = new IpcScreenshotCaptureRequest(
                CaptureId,
                IpcScreenshotTarget.Game,
                1,
                null),
            "request-scene-size" => () => _ = new IpcScreenshotCaptureRequest(
                CaptureId,
                IpcScreenshotTarget.Scene,
                1,
                1),
            "capture-size-mode" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.CurrentSurface,
                1,
                1,
                1,
                1,
                IpcScreenshotColorSpace.Linear,
                CreateState()),
            "capture-dimensions" => () => _ = new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.RequestedResolution,
                1,
                1,
                2,
                1,
                IpcScreenshotColorSpace.Linear,
                CreateState()),
            "staging-row-stride" => () => _ = new IpcScreenshotStagingImage(
                1,
                1,
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                3,
                4),
            "staging-size" => () => _ = new IpcScreenshotStagingImage(
                1,
                1,
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                4,
                5),
            "response-empty-id" => () => _ = new IpcScreenshotCaptureResponse(Guid.Empty, capture, staging),
            "response-dimensions" => () => _ = new IpcScreenshotCaptureResponse(
                CaptureId,
                capture,
                new IpcScreenshotStagingImage(
                    2,
                    1,
                    IpcScreenshotPixelFormat.Rgba8Srgb,
                    IpcScreenshotRowOrder.TopDown,
                    8,
                    8)),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown case name."),
        };

        Assert.ThrowsAny<ArgumentException>(construction);
    }

    private static (object Contract, Type ContractType) CreateContract (string contractName)
    {
        object contract = contractName switch
        {
            "request" => new IpcScreenshotCaptureRequest(
                CaptureId,
                IpcScreenshotTarget.Game,
                null,
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
            null,
            1,
            1,
            IpcScreenshotColorSpace.Linear,
            CreateState());
    }

    private static IpcScreenshotStagingImage CreateStaging ()
    {
        return new IpcScreenshotStagingImage(
            1,
            1,
            IpcScreenshotPixelFormat.Rgba8Srgb,
            IpcScreenshotRowOrder.TopDown,
            4,
            4);
    }

    private static UnityEditorStateSnapshot CreateState ()
    {
        return new UnityEditorStateSnapshot(
            DaemonEditorMode.Gui,
            IpcEditorLifecycleState.Ready,
            IpcCompileState.Ready,
            new IpcUnityGenerationSnapshot(1, 2, 3, 4),
            new IpcPlayModeSnapshot(
                IpcPlayModeState.Stopped,
                IpcPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false));
    }
}
