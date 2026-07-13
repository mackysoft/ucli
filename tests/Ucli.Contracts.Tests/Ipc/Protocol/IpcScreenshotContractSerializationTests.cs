using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcScreenshotContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ScreenshotContracts_SerializeWithCamelCaseFields ()
    {
        var request = IpcPayloadCodec.SerializeToElement(new IpcScreenshotCaptureRequest(
            Target: IpcScreenshotTarget.Game,
            RequestedWidth: 1920,
            RequestedHeight: 1080,
            StagingPath: "/tmp/ucli-screenshot.raw",
            TimeoutMilliseconds: 30000));
        var response = IpcPayloadCodec.SerializeToElement(new IpcScreenshotCaptureResponse(
            Capture: new IpcScreenshotCapture(
                target: IpcScreenshotTarget.Game,
                sizeMode: IpcScreenshotSizeMode.RequestedResolution,
                requestedWidth: 1920,
                requestedHeight: 1080,
                width: 1920,
                height: 1080,
                colorSpace: IpcScreenshotColorSpace.Linear,
                state: new UnityEditorStateSnapshot(
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
                Path: "/tmp/ucli-screenshot.raw",
                PixelFormat: IpcScreenshotPixelFormat.Rgba8Srgb,
                RowOrder: IpcScreenshotRowOrder.TopDown,
                RowStrideBytes: 7680,
                SizeBytes: 8294400)));

        JsonAssert.For(request)
            .HasString("target", "game")
            .HasInt32("requestedWidth", 1920)
            .HasInt32("requestedHeight", 1080)
            .HasString("stagingPath", "/tmp/ucli-screenshot.raw")
            .HasInt32("timeoutMilliseconds", 30000);
        JsonAssert.For(response)
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
                .HasString("path", "/tmp/ucli-screenshot.raw")
                .HasString("pixelFormat", "rgba8Srgb")
                .HasString("rowOrder", "topDown")
                .HasInt32("rowStrideBytes", 7680)
                .HasInt32("sizeBytes", 8294400));
    }
}
