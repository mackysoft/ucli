using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary> Interprets uCLI nullable Int32 range declarations as exact provider metadata. </summary>
public sealed class UcliNullableInt32RangeAttributeInterpreter :
    IJsonContractAttributeInterpreter<UcliInt32RangeAttribute, int?>
{
    /// <inheritdoc />
    public string StableId => "mackysoft.ucli.nullable-int32-range";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public void InterpretAttribute (
        UcliInt32RangeAttribute attribute,
        JsonContractMetadataContext<int?> context,
        JsonContractMetadataBuilder<int?> builder)
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
        builder.SetMaximum(JsonContractNumber.FromInt64(attribute.Maximum));
    }
}
