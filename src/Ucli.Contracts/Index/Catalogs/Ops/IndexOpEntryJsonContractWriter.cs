using System.Text.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes full operation detail contracts with a fixed public JSON shape. </summary>
internal static class IndexOpEntryJsonContractWriter
{
    internal static void WriteEntry (
        Utf8JsonWriter writer,
        IndexOpEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteDescriptorIdentity(writer, entry);
        WriteNullableString(writer, "descriptorDigest", entry.DescriptorDigest?.ToString());
        WriteDescriptorBody(writer, entry);
        writer.WriteEndObject();
    }

    internal static void WriteDescriptorDigestInput (
        Utf8JsonWriter writer,
        IndexOpEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteDescriptorIdentity(writer, entry);
        WriteDescriptorBody(writer, entry);
        writer.WriteEndObject();
    }

    private static void WriteDescriptorIdentity (
        Utf8JsonWriter writer,
        IndexOpEntryJsonContract entry)
    {
        WriteNullableString(writer, "name", entry.Name);
        WriteNullableVocabulary(writer, "kind", entry.Kind);
        WriteNullableVocabulary(writer, "policy", entry.Policy);
        if (entry.Exposure.HasValue)
        {
            WriteNullableVocabulary(writer, "exposure", entry.Exposure);
        }
        WriteNullableVocabulary(writer, "playModeSupport", entry.PlayModeSupport);
    }

    private static void WriteDescriptorBody (
        Utf8JsonWriter writer,
        IndexOpEntryJsonContract entry)
    {
        WriteNullableString(writer, "description", entry.Description);
        WriteGeneratedContract(writer, "argsContract", entry.ArgsContract);
        WriteGeneratedContract(writer, "resultContract", entry.ResultContract);
        WriteVerdictContract(writer, entry.VerdictContract);
        writer.WritePropertyName("assurance");
        JsonSerializer.Serialize(
            writer,
            entry.Assurance,
            IndexJsonContractSerializerOptions.Deserialize);
        WriteOperationCodeContract(writer, entry.CodeContract);
    }

    private static void WriteVerdictContract (
        Utf8JsonWriter writer,
        UcliOperationVerdictContract? verdictContract)
    {
        writer.WritePropertyName("verdictContract");
        if (verdictContract == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteNullableString(writer, "description", verdictContract.Description);
        writer.WriteEndObject();
    }

    private static void WriteGeneratedContract (
        Utf8JsonWriter writer,
        string propertyName,
        UcliOperationJsonContract? contract)
    {
        writer.WritePropertyName(propertyName);
        if (contract == null)
        {
            writer.WriteNullValue();
            return;
        }

        var generatedContract = contract.Value;
        writer.WriteStartObject();
        writer.WriteString("contractDigest", generatedContract.ContractDigest.ToString());
        writer.WritePropertyName("typeMetadata");
        generatedContract.TypeMetadata.WriteTo(writer);
        writer.WritePropertyName("schema");
        generatedContract.Schema.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static void WriteNullableString (
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (value == null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, value);
    }

    private static void WriteNullableVocabulary<T> (
        Utf8JsonWriter writer,
        string propertyName,
        T? value)
        where T : struct, Enum
    {
        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(
            writer,
            value,
            IndexJsonContractSerializerOptions.Deserialize);
    }

    private static void WriteArray<TItem> (
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<TItem>? items,
        Action<Utf8JsonWriter, TItem> writeItem)
    {
        writer.WritePropertyName(propertyName);
        if (items == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        for (var i = 0; i < items.Count; i++)
        {
            writeItem(writer, items[i]);
        }

        writer.WriteEndArray();
    }

    private static void WriteStringArray (
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string>? values)
    {
        writer.WritePropertyName(propertyName);
        if (values == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteStringValue(values[i]);
        }

        writer.WriteEndArray();
    }

    private static void WriteOperationCodeContract (
        Utf8JsonWriter writer,
        UcliOperationCodeContract? codeContract)
    {
        if (codeContract == null)
        {
            return;
        }

        writer.WritePropertyName("codeContract");
        writer.WriteStartObject();
        WriteNullableString(
            writer,
            "language",
            codeContract.Language.HasValue
                ? TextVocabulary.GetText(codeContract.Language.Value)
                : null);
        WriteCodeEntryPoint(writer, codeContract.EntryPoint);
        WriteArray(writer, "sourceForms", codeContract.SourceForms, WriteCodeSourceForm);
        WriteArray(writer, "apiTypes", codeContract.ApiTypes, WriteCodeApiType);
        writer.WriteEndObject();
    }

    private static void WriteCodeEntryPoint (
        Utf8JsonWriter writer,
        UcliCodeEntryPointContract? entryPoint)
    {
        writer.WritePropertyName("entryPoint");
        if (entryPoint == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteNullableString(writer, "signature", entryPoint.Signature);
        WriteNullableString(writer, "matchRule", entryPoint.MatchRule);
        writer.WriteBoolean("requiredStatic", entryPoint.RequiredStatic);
        WriteStringArray(writer, "parameterTypes", entryPoint.ParameterTypes);
        WriteNullableString(writer, "returnValue", entryPoint.ReturnValue);
        writer.WriteEndObject();
    }

    private static void WriteCodeSourceForm (
        Utf8JsonWriter writer,
        UcliCodeSourceFormContract sourceForm)
    {
        writer.WriteStartObject();
        WriteNullableString(
            writer,
            "kind",
            sourceForm.Kind.HasValue
                ? TextVocabulary.GetText(sourceForm.Kind.Value)
                : null);
        WriteNullableString(writer, "description", sourceForm.Description);
        writer.WriteEndObject();
    }

    private static void WriteCodeApiType (
        Utf8JsonWriter writer,
        UcliCodeApiTypeContract apiType)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", apiType.Name);
        WriteNullableString(writer, "fullName", apiType.FullName);
        WriteNullableString(writer, "description", apiType.Description);
        WriteArray(writer, "members", apiType.Members, WriteCodeApiMember);
        writer.WriteEndObject();
    }

    private static void WriteCodeApiMember (
        Utf8JsonWriter writer,
        UcliCodeApiMemberContract member)
    {
        writer.WriteStartObject();
        WriteNullableString(
            writer,
            "kind",
            member.Kind.HasValue
                ? TextVocabulary.GetText(member.Kind.Value)
                : null);
        WriteNullableString(writer, "name", member.Name);
        WriteNullableString(writer, "description", member.Description);
        WriteNullableString(writer, "type", member.Type);
        WriteNullableString(writer, "returnType", member.ReturnType);
        WriteArray(writer, "parameters", member.Parameters, WriteCodeApiParameter);
        writer.WriteEndObject();
    }

    private static void WriteCodeApiParameter (
        Utf8JsonWriter writer,
        UcliCodeApiParameterContract parameter)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", parameter.Name);
        WriteNullableString(writer, "type", parameter.Type);
        WriteNullableString(writer, "description", parameter.Description);
        writer.WriteEndObject();
    }
}
