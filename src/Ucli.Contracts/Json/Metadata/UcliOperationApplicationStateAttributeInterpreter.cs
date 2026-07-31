using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary>
/// Interprets operation application-state declarations as the exact
/// TextVocabulary-derived schema subset.
/// </summary>
public sealed class UcliOperationApplicationStateAttributeInterpreter :
    IJsonContractAttributeInterpreter<
        UcliOperationApplicationStateAttribute,
        ExecutionApplicationState>
{
    private static readonly ExecutionApplicationState[] AllowedValues =
        Enum.GetValues(typeof(ExecutionApplicationState))
            .Cast<ExecutionApplicationState>()
            .Where(ExecutionApplicationStateSemantics.IsOperationState)
            .ToArray();

    /// <inheritdoc />
    public string StableId =>
        "mackysoft.ucli.operation-application-state";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public void InterpretAttribute (
        UcliOperationApplicationStateAttribute attribute,
        JsonContractMetadataContext<ExecutionApplicationState> context,
        JsonContractMetadataBuilder<ExecutionApplicationState> builder)
    {
        if (attribute == null)
        {
            throw new ArgumentNullException(nameof(attribute));
        }
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.SetPattern(
            TextVocabularySubsetPattern.Create(AllowedValues));
    }
}
