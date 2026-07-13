using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Represents the result of one screenshot capture workflow. </summary>
internal sealed record ScreenshotCaptureResult
{
    private ScreenshotCaptureResult (
        ScreenshotCaptureOutput? output,
        ExecutionError? error)
    {
        Output = output;
        Error = error;
    }

    /// <summary> Gets a value indicating whether the workflow succeeded. </summary>
    [MemberNotNullWhen(true, nameof(Output))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Output is not null;

    /// <summary> Gets the capture output on success; otherwise <see langword="null" />. </summary>
    public ScreenshotCaptureOutput? Output { get; }

    /// <summary> Gets the structured error on failure; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Creates a successful screenshot result. </summary>
    public static ScreenshotCaptureResult Success (ScreenshotCaptureOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new ScreenshotCaptureResult(output, error: null);
    }

    /// <summary> Creates a failed screenshot result. </summary>
    public static ScreenshotCaptureResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ScreenshotCaptureResult(output: null, error);
    }
}
