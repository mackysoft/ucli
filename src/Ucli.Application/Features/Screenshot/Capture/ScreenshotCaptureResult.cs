using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Represents the result of one screenshot capture workflow. </summary>
internal sealed record ScreenshotCaptureResult
{
    private ScreenshotCaptureResult (
        ScreenshotCaptureOutput? output,
        ExecutionError? error,
        ScreenshotCaptureFailureDisposition failureDisposition)
    {
        Output = output;
        Error = error;
        FailureDisposition = failureDisposition;
    }

    /// <summary> Gets a value indicating whether the workflow succeeded. </summary>
    [MemberNotNullWhen(true, nameof(Output))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Output is not null;

    /// <summary> Gets the capture output on success; otherwise <see langword="null" />. </summary>
    public ScreenshotCaptureOutput? Output { get; }

    /// <summary> Gets the structured error on failure; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary>
    /// Gets whether a failed capture reached a typed, terminal outcome. A
    /// transport loss or malformed provider response must remain recoverable
    /// to its caller instead of being projected as a capture failure.
    /// </summary>
    public ScreenshotCaptureFailureDisposition FailureDisposition { get; }

    /// <summary> Creates a successful screenshot result. </summary>
    public static ScreenshotCaptureResult Success (ScreenshotCaptureOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new ScreenshotCaptureResult(output, error: null, ScreenshotCaptureFailureDisposition.Terminal);
    }

    /// <summary> Creates a failed screenshot result. </summary>
    public static ScreenshotCaptureResult Failure (
        ExecutionError error,
        ScreenshotCaptureFailureDisposition failureDisposition = ScreenshotCaptureFailureDisposition.Terminal)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!Enum.IsDefined(failureDisposition))
        {
            throw new ArgumentOutOfRangeException(nameof(failureDisposition));
        }
        return new ScreenshotCaptureResult(output: null, error, failureDisposition);
    }
}

/// <summary> Classifies a failed capture without guessing whether Unity completed it. </summary>
internal enum ScreenshotCaptureFailureDisposition
{
    /// <summary> Unity or local validation returned a typed failure outcome. </summary>
    Terminal = 1,

    /// <summary> The response was lost before a capture outcome could be established. </summary>
    CommunicationLost,

    /// <summary> The provider response cannot be accepted as the capture contract. </summary>
    ContractInvalid,
}
