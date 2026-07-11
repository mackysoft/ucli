using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcScreenshotContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ScreenshotContracts_SerializeWithCamelCaseFields ()
    {
        var request = IpcPayloadCodec.SerializeToElement(new IpcScreenshotCaptureRequest(
            Target: ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
            RequestedWidth: 1920,
            RequestedHeight: 1080,
            StagingPath: "/tmp/ucli-screenshot.raw",
            TimeoutMilliseconds: 30000));
        var response = IpcPayloadCodec.SerializeToElement(new IpcScreenshotCaptureResponse(
            Capture: new IpcScreenshotCapture(
                Target: ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
                SizeMode: ContractLiteralCodec.ToValue(IpcScreenshotSizeMode.RequestedResolution),
                RequestedWidth: 1920,
                RequestedHeight: 1080,
                Width: 1920,
                Height: 1080,
                ColorSpace: ContractLiteralCodec.ToValue(IpcScreenshotColorSpace.Linear),
                LifecycleStateAtCapture: "ready",
                CompileStateAtCapture: IpcCompileStateCodec.Ready,
                DomainReloadGeneration: 7,
                PlayModeState: ContractLiteralCodec.ToValue(IpcPlayModeState.Playing)),
            Staging: new IpcScreenshotStagingImage(
                Path: "/tmp/ucli-screenshot.raw",
                PixelFormat: ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
                RowOrder: ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
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
                .HasString("lifecycleStateAtCapture", "ready")
                .HasString("compileStateAtCapture", "ready")
                .HasInt32("domainReloadGeneration", 7)
                .HasString("playModeState", "playing"))
            .HasProperty("staging", staging => staging
                .HasString("path", "/tmp/ucli-screenshot.raw")
                .HasString("pixelFormat", "rgba8Srgb")
                .HasString("rowOrder", "topDown")
                .HasInt32("rowStrideBytes", 7680)
                .HasInt32("sizeBytes", 8294400));
    }
}
