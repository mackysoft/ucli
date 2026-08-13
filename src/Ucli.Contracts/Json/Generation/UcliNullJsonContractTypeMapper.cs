using MackySoft.JsonSchema.Generation.ContractModel;
using MackySoft.JsonSchema.Generation.Extensibility;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary> Maps the literal-null marker to the JSON Schema null scalar. </summary>
internal sealed class UcliNullJsonContractTypeMapper : IJsonContractTypeMapper
{
    public string StableId => "ucli.literal-null";

    public string ContractVersion => "1";

    public bool CanMap (JsonContractTypeMapperContext context)
    {
        var converter = context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
        return context.TypeInfo.Type == typeof(UcliNull) && converter is UcliNullJsonConverter;
    }

    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context) =>
        JsonContractTypeMapping.Scalar(JsonContractScalarKind.Null);
}
