using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Presentation;

namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Represents normalized screenshot command options. </summary>
internal sealed record ScreenshotCommandOptionsNormalizationResult (
    PixelDimensions? RequestedDimensions,
    int? TimeoutMilliseconds,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether all options are valid. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful option result. </summary>
    public static ScreenshotCommandOptionsNormalizationResult Success (
        PixelDimensions? requestedDimensions,
        int? timeoutMilliseconds)
    {
        return new ScreenshotCommandOptionsNormalizationResult(
            requestedDimensions,
            timeoutMilliseconds,
            Error: null);
    }

    /// <summary> Creates a failed option result. </summary>
    public static ScreenshotCommandOptionsNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ScreenshotCommandOptionsNormalizationResult(null, null, error);
    }
}
