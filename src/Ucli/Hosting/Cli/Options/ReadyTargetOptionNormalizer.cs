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
        if (optionValue is null)
        {
            return ReadyTargetOptionNormalizationResult.Success(ReadyTarget.Execution);
        }

        var normalizedValue = optionValue.Trim();
        return ReadyTargetCodec.TryParseValue(normalizedValue, out var target)
            ? ReadyTargetOptionNormalizationResult.Success(target)
            : ReadyTargetOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(InvalidTargetMessage));
    }
}
