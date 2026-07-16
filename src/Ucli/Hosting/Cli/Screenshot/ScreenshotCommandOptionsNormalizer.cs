using System.Globalization;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Normalizes screenshot command options before application dispatch. </summary>
internal static class ScreenshotCommandOptionsNormalizer
{
    /// <summary> Normalizes GameView screenshot options. </summary>
    public static ScreenshotCommandOptionsNormalizationResult NormalizeGame (
        string? width,
        string? height,
        string? timeout)
    {
        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            return ScreenshotCommandOptionsNormalizationResult.Failure(timeoutResult.Error!);
        }

        if (width is null && height is null)
        {
            return ScreenshotCommandOptionsNormalizationResult.Success(
                requestedWidth: null,
                requestedHeight: null,
                timeoutResult.TimeoutMilliseconds);
        }

        if (!TryParsePositiveInteger(width, out var requestedWidth)
            || !TryParsePositiveInteger(height, out var requestedHeight))
        {
            return ScreenshotCommandOptionsNormalizationResult.Failure(ExecutionError.InvalidArgument(
                "width and height must be specified together as positive integers."));
        }

        return ScreenshotCommandOptionsNormalizationResult.Success(
            requestedWidth,
            requestedHeight,
            timeoutResult.TimeoutMilliseconds);
    }

    private static bool TryParsePositiveInteger (string? value, out int parsedValue)
    {
        if (value is null)
        {
            parsedValue = default;
            return false;
        }

        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out parsedValue)
            && parsedValue > 0;
    }
}
