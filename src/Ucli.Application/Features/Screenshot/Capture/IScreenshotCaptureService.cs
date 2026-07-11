namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Captures Unity Editor presentation surfaces as PNG artifacts. </summary>
internal interface IScreenshotCaptureService
{
    /// <summary> Captures one presentation surface and commits its PNG artifact. </summary>
    ValueTask<ScreenshotCaptureResult> CaptureAsync (
        ScreenshotCaptureInput input,
        CancellationToken cancellationToken = default);
}
