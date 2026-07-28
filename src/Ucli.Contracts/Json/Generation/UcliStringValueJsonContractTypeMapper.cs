using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.ContractModel;
using MackySoft.JsonSchema.Generation.Extensibility;

namespace MackySoft.Ucli.Contracts.Json.Generation;

internal sealed class UcliStringValueJsonContractTypeMapper : IJsonContractTypeMapper
{
    public string StableId => "ucli.semantic-string";

    public string ContractVersion => "1";

    public bool CanMap (JsonContractTypeMapperContext context)
    {
        return UcliStringValue.IsAssignableFrom(context.TypeInfo.Type)
            && IsStringValueConverter(GetEffectiveConverter(context));
    }

    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context)
    {
        return JsonContractTypeMapping.Scalar(JsonContractScalarKind.String);
    }

    private static JsonConverter GetEffectiveConverter (JsonContractTypeMapperContext context)
    {
        return context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
    }

    private static bool IsStringValueConverter (JsonConverter effectiveConverter)
    {
        return effectiveConverter is UcliStringValueJsonConverterFactory
            || effectiveConverter.GetType().DeclaringType == typeof(UcliStringValueJsonConverterFactory);
    }
}
