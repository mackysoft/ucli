using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

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

        if (ContractLiteralInputParser.TryParseIgnoreCase<OperationPolicy>(optionValue, out var policy))
        {
            return OperationPolicyOptionNormalizationResult.Success(policy);
        }

        return OperationPolicyOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"maxPolicy must be one of '{ContractLiteralCodec.ToValue(OperationPolicy.Safe)}', '{ContractLiteralCodec.ToValue(OperationPolicy.Advanced)}', '{ContractLiteralCodec.ToValue(OperationPolicy.Dangerous)}'. Actual: {optionValue}."));
    }
}
