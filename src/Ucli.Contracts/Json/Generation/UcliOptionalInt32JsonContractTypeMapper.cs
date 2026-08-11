using MackySoft.JsonSchema.Generation.Extensibility;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>Maps the optional Int32 value representation to its supplied JSON-number contract.</summary>
internal sealed class UcliOptionalInt32JsonContractTypeMapper : IJsonContractTypeMapper
{
    public string StableId => "ucli.optional-int32";

    public string ContractVersion => "1";

    public bool CanMap (JsonContractTypeMapperContext context)
    {
        var effectiveConverter =
            context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
        return context.TypeInfo.Type == typeof(UcliOptionalInt32)
            && effectiveConverter is UcliOptionalInt32JsonConverter;
    }

    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context) =>
        JsonContractTypeMapping.ContractType(typeof(int));
}
