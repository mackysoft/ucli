using MackySoft.Ucli.Application.Features.Screenshot.Capture;

namespace MackySoft.Ucli.Tests;

internal sealed class RecordingScreenshotCaptureService : IScreenshotCaptureService
{
    private readonly Func<ScreenshotCaptureInput, CancellationToken, ValueTask<ScreenshotCaptureResult>> handler;

    public RecordingScreenshotCaptureService (
        Func<ScreenshotCaptureInput, CancellationToken, ValueTask<ScreenshotCaptureResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public List<ScreenshotCaptureInput> Inputs { get; } = [];

    public ValueTask<ScreenshotCaptureResult> CaptureAsync (
        ScreenshotCaptureInput input,
        CancellationToken cancellationToken = default)
    {
        Inputs.Add(input);
        return handler(input, cancellationToken);
    }
}
