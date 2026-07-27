using System.Text.Json;
using MackySoft.JsonSchema.Generation.Extensibility;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary> Maps <see cref="UcliJsonObject" /> to a non-null JSON object contract. </summary>
internal sealed class UcliJsonObjectJsonContractTypeMapper : IJsonContractTypeMapper
{
    /// <inheritdoc />
    public string StableId => "ucli.json-object";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public bool CanMap (JsonContractTypeMapperContext context)
    {
        var effectiveConverter =
            context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
        return context.TypeInfo.Type == typeof(UcliJsonObject)
            && effectiveConverter.GetType() == typeof(UcliJsonObjectJsonConverter);
    }

    /// <inheritdoc />
    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context)
    {
        return JsonContractTypeMapping.ContractType(
            typeof(Dictionary<string, JsonElement>));
    }
}
