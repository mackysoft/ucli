using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary> Interprets uCLI Boolean constant declarations as provider metadata. </summary>
public sealed class UcliBooleanConstantAttributeInterpreter :
    IJsonContractAttributeInterpreter<UcliBooleanConstantAttribute, bool>
{
    /// <inheritdoc />
    public string StableId => "mackysoft.ucli.boolean-constant";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public void InterpretAttribute (
        UcliBooleanConstantAttribute attribute,
        JsonContractMetadataContext<bool> context,
        JsonContractMetadataBuilder<bool> builder)
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

        builder.SetConst(attribute.Value);
    }
}
