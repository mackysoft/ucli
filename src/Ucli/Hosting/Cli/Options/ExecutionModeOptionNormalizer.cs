using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--mode</c> option into a typed override. </summary>
internal static class ExecutionModeOptionNormalizer
{
    /// <summary> Normalizes one optional <c>--mode</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static ExecutionModeOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return ExecutionModeOptionNormalizationResult.Omitted();
        }

        if (UnityExecutionModeCodec.TryParse(optionValue, out var mode))
        {
            return ExecutionModeOptionNormalizationResult.Success(mode);
        }

        return ExecutionModeOptionNormalizationResult.Failure(
            UnityExecutionModeDecisionResultFactory.CreateInvalidModeError());
    }

}