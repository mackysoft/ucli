using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.Text.Vocabularies.Json;

namespace MackySoft.Ucli.Contracts.Json.Generation;

internal sealed class UcliVocabularyJsonContractTypeMapper : IJsonContractTypeMapper
{
    public string StableId => "ucli.text-vocabulary";

    public string ContractVersion => "1";

    public bool CanMap (JsonContractTypeMapperContext context)
    {
        return TextVocabulary.IsVocabulary(context.TypeInfo.Type)
            && IsVocabularyConverter(GetEffectiveConverter(context));
    }

    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context)
    {
        return JsonContractTypeMapping.TextVocabulary();
    }

    private static JsonConverter GetEffectiveConverter (JsonContractTypeMapperContext context)
    {
        return context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
    }

    private static bool IsVocabularyConverter (JsonConverter effectiveConverter)
    {
        return effectiveConverter is VocabularyJsonConverterFactory
            || effectiveConverter.GetType().DeclaringType == typeof(VocabularyJsonConverterFactory);
    }
}
