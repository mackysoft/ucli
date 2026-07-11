using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Represents the result of one screenshot capture workflow. </summary>
/// <param name="Output"> The capture output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record ScreenshotCaptureResult (
    ScreenshotCaptureOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the workflow succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful screenshot result. </summary>
    public static ScreenshotCaptureResult Success (ScreenshotCaptureOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new ScreenshotCaptureResult(output, null);
    }

    /// <summary> Creates a failed screenshot result. </summary>
    public static ScreenshotCaptureResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ScreenshotCaptureResult(null, error);
    }
}
