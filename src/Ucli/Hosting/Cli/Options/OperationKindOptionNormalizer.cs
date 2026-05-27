using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes CLI operation-kind options into typed filters. </summary>
internal static class OperationKindOptionNormalizer
{
    /// <summary> Normalizes one optional operation-kind value. </summary>
    public static OperationKindOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return OperationKindOptionNormalizationResult.Success(kind: null);
        }

        if (ContractLiteralInputParser.TryParseIgnoreCase<UcliOperationKind>(optionValue, out var kind))
        {
            return OperationKindOptionNormalizationResult.Success(kind);
        }

        return OperationKindOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"kind must be one of '{ContractLiteralCodec.ToValue(UcliOperationKind.Query)}', '{ContractLiteralCodec.ToValue(UcliOperationKind.Mutation)}', '{ContractLiteralCodec.ToValue(UcliOperationKind.Command)}'. Actual: {optionValue}."));
    }
}
