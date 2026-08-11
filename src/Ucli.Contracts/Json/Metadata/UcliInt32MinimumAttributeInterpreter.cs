using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary> Interprets uCLI Int32 minimum declarations as exact provider metadata. </summary>
public sealed class UcliInt32MinimumAttributeInterpreter :
    IJsonContractAttributeInterpreter<UcliInt32MinimumAttribute, int>
{
    /// <inheritdoc />
    public string StableId => "mackysoft.ucli.int32-minimum";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public void InterpretAttribute (
        UcliInt32MinimumAttribute attribute,
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

        builder.SetMinimum(JsonContractNumber.FromInt64(attribute.Minimum));
    }
}
