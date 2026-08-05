using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary>Interprets minimum declarations for a supplied optional Int32 value.</summary>
internal sealed class UcliOptionalInt32MinimumAttributeInterpreter :
    IJsonContractAttributeInterpreter<UcliInt32MinimumAttribute, UcliOptionalInt32>
{
    public string StableId => "mackysoft.ucli.optional-int32-minimum";

    public string ContractVersion => "1";

    public void InterpretAttribute (
        UcliInt32MinimumAttribute attribute,
        JsonContractMetadataContext<UcliOptionalInt32> context,
        JsonContractMetadataBuilder<UcliOptionalInt32> builder)
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
