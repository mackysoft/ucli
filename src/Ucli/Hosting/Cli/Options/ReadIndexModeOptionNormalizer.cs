using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--readIndexMode</c> option into a typed override. </summary>
internal static class ReadIndexModeOptionNormalizer
{
    /// <summary> Normalizes one optional <c>--readIndexMode</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static ReadIndexModeOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return ReadIndexModeOptionNormalizationResult.Success(mode: null);
        }

        if (ReadIndexModeCodec.TryParse(optionValue, out var mode))
        {
            return ReadIndexModeOptionNormalizationResult.Success(mode);
        }

        return ReadIndexModeOptionNormalizationResult.Failure(Shared.Foundation.ExecutionError.InvalidArgument(
            $"readIndexMode must be one of '{ReadIndexModeValues.Disabled}', '{ReadIndexModeValues.AllowStale}', '{ReadIndexModeValues.RequireFresh}'. Actual: {optionValue}."));
    }
}