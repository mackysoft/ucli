using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

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

        if (ReadIndexModeCodec.TryParse(optionValue, out var mode))
        {
            return ReadIndexModeResolutionResult.Success(mode);
        }

        return ReadIndexModeResolutionResult.Failure(ExecutionError.InvalidArgument(
            $"readIndexMode must be one of '{ReadIndexModeValues.Disabled}', '{ReadIndexModeValues.AllowStale}', '{ReadIndexModeValues.RequireFresh}'. Actual: {optionValue}."));
    }

    /// <summary> Resolves effective read-index mode from optional typed command value and config defaults. </summary>
    /// <param name="optionValue"> The optional normalized command option value. </param>
    /// <param name="config"> The loaded config values. </param>
    /// <returns> The mode-resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public static ReadIndexModeResolutionResult Resolve (
        ReadIndexMode? optionValue,
        UcliConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return ReadIndexModeResolutionResult.Success(optionValue ?? config.ReadIndexDefaultMode);
    }

}
