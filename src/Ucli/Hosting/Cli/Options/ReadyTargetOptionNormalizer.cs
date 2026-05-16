using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--for</c> option for <c>ready</c>. </summary>
internal static class ReadyTargetOptionNormalizer
{
    private const string InvalidTargetMessage = "ready --for must be one of execution, mutation, test, or readIndex.";

    /// <summary> Normalizes one optional <c>--for</c> value. </summary>
    public static ReadyTargetOptionNormalizationResult Normalize (string? optionValue)
    {
        if (string.IsNullOrWhiteSpace(optionValue))
        {
            return ReadyTargetOptionNormalizationResult.Success(ReadyTarget.Execution);
        }

        if (ReadyTargetCodec.TryParse(optionValue, out var target))
        {
            return ReadyTargetOptionNormalizationResult.Success(target);
        }

        return ReadyTargetOptionNormalizationResult.Failure(
            ExecutionError.InvalidArgument(InvalidTargetMessage));
    }
}
