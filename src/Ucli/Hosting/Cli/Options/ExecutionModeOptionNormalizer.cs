using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--mode</c> option into a typed override. </summary>
internal static class ExecutionModeOptionNormalizer
{
    private const string InvalidModeMessage = "Mode must be auto, daemon, or oneshot.";

    /// <summary> Normalizes one optional <c>--mode</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static ExecutionModeOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return ExecutionModeOptionNormalizationResult.Omitted();
        }

        if (!string.IsNullOrWhiteSpace(optionValue)
            && VocabularyInputParser.TryParseIgnoreCase(optionValue.Trim(), out UnityExecutionMode mode))
        {
            return ExecutionModeOptionNormalizationResult.Success(mode);
        }

        return ExecutionModeOptionNormalizationResult.Failure(
            ExecutionError.InvalidArgument(InvalidModeMessage));
    }
}
