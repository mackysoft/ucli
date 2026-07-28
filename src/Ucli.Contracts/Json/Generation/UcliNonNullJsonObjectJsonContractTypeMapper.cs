using System.Text.Json;
using MackySoft.JsonSchema.Generation.Extensibility;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>
/// Maps the runtime non-null object wrapper to its authoritative serializer DTO contract.
/// </summary>
internal sealed class UcliNonNullJsonObjectJsonContractTypeMapper : IJsonContractTypeMapper
{
    /// <inheritdoc />
    public string StableId => "ucli.non-null-json-object";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public bool CanMap (JsonContractTypeMapperContext context)
    {
        var effectiveConverter =
            context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
        return (context.TypeInfo.Type == typeof(IUcliNonNullJsonObject)
                || UcliNonNullJsonObject.IsValueType(context.TypeInfo.Type))
            && UcliNonNullJsonObject.IsValueConverter(effectiveConverter);
    }

    /// <inheritdoc />
    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context)
    {
        return context.TypeInfo.Type == typeof(IUcliNonNullJsonObject)
            ? JsonContractTypeMapping.ContractType(
                typeof(Dictionary<string, JsonElement>))
            : JsonContractTypeMapping.ContractType(
                context.TypeInfo.Type.GetGenericArguments()[0]);
    }
}
