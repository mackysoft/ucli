using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes CLI maximum operation-policy options into typed filters. </summary>
internal static class OperationMaxPolicyOptionNormalizer
{
    /// <summary> Normalizes one optional maximum operation-policy value. </summary>
    public static OperationPolicyOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return OperationPolicyOptionNormalizationResult.Success(policy: null);
        }

        if (OperationPolicyCodec.TryParse(optionValue, out var policy))
        {
            return OperationPolicyOptionNormalizationResult.Success(policy);
        }

        return OperationPolicyOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"maxPolicy must be one of '{OperationPolicyValues.Safe}', '{OperationPolicyValues.Advanced}', '{OperationPolicyValues.Dangerous}'. Actual: {optionValue}."));
    }
}
