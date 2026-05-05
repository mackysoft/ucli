using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Resolves effective read-index mode from command options and config defaults. </summary>
internal static class ReadIndexModeResolver
{
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
