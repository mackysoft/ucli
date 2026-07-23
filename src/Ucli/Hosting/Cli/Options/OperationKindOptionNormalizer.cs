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

        if (VocabularyInputParser.TryParseIgnoreCase<UcliOperationKind>(optionValue, out var kind))
        {
            return OperationKindOptionNormalizationResult.Success(kind);
        }

        return OperationKindOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"kind must be one of '{TextVocabulary.GetText(UcliOperationKind.Query)}', '{TextVocabulary.GetText(UcliOperationKind.Mutation)}', '{TextVocabulary.GetText(UcliOperationKind.Command)}'. Actual: {optionValue}."));
    }
}
