using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.ContractModel;
using MackySoft.JsonSchema.Generation.Extensibility;

namespace MackySoft.Ucli.Contracts.Json.Generation;

internal sealed class UcliExactConverterScalarJsonContractTypeMapper : IJsonContractTypeMapper
{
    private readonly Type targetType;
    private readonly Type converterType;
    private readonly JsonContractScalarKind scalarKind;

    public UcliExactConverterScalarJsonContractTypeMapper (
        string stableId,
        Type targetType,
        Type converterType,
        JsonContractScalarKind scalarKind)
    {
        StableId = stableId ?? throw new ArgumentNullException(nameof(stableId));
        this.targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        this.converterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
        this.scalarKind = scalarKind;
    }

    public string StableId { get; }

    public string ContractVersion => "1";

    public bool CanMap (JsonContractTypeMapperContext context)
    {
        return context.TypeInfo.Type == targetType
            && IsExactConverter(context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter);
    }

    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context)
    {
        return JsonContractTypeMapping.Scalar(scalarKind);
    }

    private bool IsExactConverter (JsonConverter effectiveConverter)
    {
        return effectiveConverter.GetType() == converterType;
    }
}
