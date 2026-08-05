using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary> Interprets uCLI Int32 constant declarations as exact provider metadata. </summary>
public sealed class UcliInt32ConstantAttributeInterpreter :
    IJsonContractAttributeInterpreter<UcliInt32ConstantAttribute, int>
{
    /// <inheritdoc />
    public string StableId => "mackysoft.ucli.int32-constant";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public void InterpretAttribute (
        UcliInt32ConstantAttribute attribute,
        JsonContractMetadataContext<int> context,
        JsonContractMetadataBuilder<int> builder)
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
