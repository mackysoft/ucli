using System.Globalization;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Normalizes screenshot command options before application dispatch. </summary>
internal static class ScreenshotCommandOptionsNormalizer
{
    /// <summary> Normalizes GameView screenshot options. </summary>
    public static ScreenshotCommandOptionsNormalizationResult NormalizeGame (
        string? mode,
        string? width,
        string? height,
        string? timeout)
    {
        var commonResult = NormalizeCommon(mode, timeout);
        if (!commonResult.IsSuccess)
        {
            return commonResult;
        }

        if (width is null && height is null)
        {
            return commonResult;
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
            commonResult.TimeoutMilliseconds);
    }

    /// <summary> Normalizes SceneView screenshot options. </summary>
    public static ScreenshotCommandOptionsNormalizationResult NormalizeScene (
        string? mode,
        string? timeout)
    {
        return NormalizeCommon(mode, timeout);
    }

    private static ScreenshotCommandOptionsNormalizationResult NormalizeCommon (
        string? mode,
        string? timeout)
    {
        var modeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!modeResult.IsSuccess)
        {
            return ScreenshotCommandOptionsNormalizationResult.Failure(modeResult.Error!);
        }

        if (modeResult.Mode == UnityExecutionMode.Oneshot)
        {
            return ScreenshotCommandOptionsNormalizationResult.Failure(ExecutionError.InvalidArgument(
                "Screenshot mode must be auto or daemon; oneshot capture is not supported."));
        }

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        return timeoutResult.IsSuccess
            ? ScreenshotCommandOptionsNormalizationResult.Success(null, null, timeoutResult.TimeoutMilliseconds)
            : ScreenshotCommandOptionsNormalizationResult.Failure(timeoutResult.Error!);
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
