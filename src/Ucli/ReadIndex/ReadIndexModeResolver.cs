using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.ReadIndex;

/// <summary> Resolves effective read-index mode from command options and config defaults. </summary>
internal static class ReadIndexModeResolver
{
    /// <summary> Resolves effective read-index mode from optional command value and config defaults. </summary>
    /// <param name="optionValue"> The optional command option value. </param>
    /// <param name="config"> The loaded config values. </param>
    /// <returns> The mode-resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public static ReadIndexModeResolutionResult Resolve (
        string? optionValue,
        UcliConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (optionValue is null)
        {
            return ReadIndexModeResolutionResult.Success(config.ReadIndexDefaultMode);
        }

        if (TryParseOptionValue(optionValue, out var mode))
        {
            return ReadIndexModeResolutionResult.Success(mode);
        }

        return ReadIndexModeResolutionResult.Failure(ExecutionError.InvalidArgument(
            $"readIndexMode must be one of '{ReadIndexModeValues.Disabled}', '{ReadIndexModeValues.AllowStale}', '{ReadIndexModeValues.RequireFresh}'. Actual: {optionValue}."));
    }

    /// <summary> Parses command option value into <see cref="ReadIndexMode" />. </summary>
    /// <param name="optionValue"> The option value. </param>
    /// <param name="mode"> The parsed mode. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryParseOptionValue (
        string optionValue,
        out ReadIndexMode mode)
    {
        if (string.Equals(optionValue, ReadIndexModeValues.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            mode = ReadIndexMode.Disabled;
            return true;
        }

        if (string.Equals(optionValue, ReadIndexModeValues.AllowStale, StringComparison.OrdinalIgnoreCase))
        {
            mode = ReadIndexMode.AllowStale;
            return true;
        }

        if (string.Equals(optionValue, ReadIndexModeValues.RequireFresh, StringComparison.OrdinalIgnoreCase))
        {
            mode = ReadIndexMode.RequireFresh;
            return true;
        }

        mode = default;
        return false;
    }
}