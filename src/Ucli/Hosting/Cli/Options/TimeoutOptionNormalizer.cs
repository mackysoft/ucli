using System.Globalization;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--timeout</c> option into a validated millisecond override. </summary>
internal static class TimeoutOptionNormalizer
{
    /// <summary> Normalizes one optional <c>--timeout</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static TimeoutOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return TimeoutOptionNormalizationResult.Success(timeoutMilliseconds: null);
        }

        if (string.IsNullOrWhiteSpace(optionValue))
        {
            return CreateFailure(optionValue);
        }

        var normalizedOptionValue = optionValue.Trim();
        if (!int.TryParse(normalizedOptionValue, NumberStyles.None, CultureInfo.InvariantCulture, out var timeoutMilliseconds))
        {
            return CreateFailure(optionValue);
        }

        if (timeoutMilliseconds <= 0)
        {
            return CreateFailure(optionValue);
        }

        return TimeoutOptionNormalizationResult.Success(timeoutMilliseconds);
    }

    private static TimeoutOptionNormalizationResult CreateFailure (string? optionValue)
    {
        return TimeoutOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"timeout must be a positive integer milliseconds value. Actual: {optionValue}."));
    }
}
