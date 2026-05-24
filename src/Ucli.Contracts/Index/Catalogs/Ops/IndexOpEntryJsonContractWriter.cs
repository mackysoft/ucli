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
        WriteNullableString(writer, "name", entry.Name);
        WriteNullableString(writer, "kind", entry.Kind);
        WriteNullableString(writer, "policy", entry.Policy);
        WriteNullableString(writer, "description", entry.Description);
        WriteArray(writer, "inputs", entry.Inputs, WriteOperationInput);
        WriteOperationResultContract(writer, entry.ResultContract);
        WriteOperationAssurance(writer, entry.Assurance);
        WriteOperationCodeContract(writer, entry.CodeContract);
        WriteSchema(writer, "argsSchema", entry.ArgsSchemaJson);
        WriteOptionalSchema(writer, "resultSchema", entry.ResultSchemaJson);
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

    private static void WriteOperationInput (
        Utf8JsonWriter writer,
        UcliOperationInputContract input)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", input.Name);
        WriteNullableString(writer, "description", input.Description);
        WriteNullableString(writer, "valueType", input.ValueType);
        WriteArray(writer, "constraints", input.Constraints, WriteOperationInputConstraint);
        if (input.ArgsPath != null)
        {
            writer.WriteString("argsPath", input.ArgsPath);
        }

        if (input.Variants != null)
        {
            WriteArray(writer, "variants", input.Variants, WriteOperationInputVariant);
        }

        writer.WriteEndObject();
    }

    private static void WriteOperationInputVariant (
        Utf8JsonWriter writer,
        UcliOperationInputVariantContract variant)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", variant.Name);
        WriteNullableString(writer, "description", variant.Description);
        WriteArray(writer, "fields", variant.Fields, WriteOperationInputVariantField);
        writer.WriteEndObject();
    }

    private static void WriteOperationInputVariantField (
        Utf8JsonWriter writer,
        UcliOperationInputVariantFieldContract field)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", field.Name);
        WriteNullableString(writer, "argsPath", field.ArgsPath);
        WriteNullableString(writer, "description", field.Description);
        WriteArray(writer, "constraints", field.Constraints, WriteOperationInputConstraint);
        writer.WriteEndObject();
    }

    private static void WriteOperationInputConstraint (
        Utf8JsonWriter writer,
        UcliOperationInputConstraintContract constraint)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "kind", constraint.Kind);
        if (constraint.AssetKind != null)
        {
            writer.WriteString("assetKind", constraint.AssetKind);
        }

        if (constraint.TargetKind != null)
        {
            writer.WriteString("targetKind", constraint.TargetKind);
        }

        if (constraint.TypeKind != null)
        {
            writer.WriteString("typeKind", constraint.TypeKind);
        }

        if (constraint.Access != null)
        {
            writer.WriteString("access", constraint.Access);
        }

        if (constraint.Min.HasValue)
        {
            writer.WriteNumber("min", constraint.Min.Value);
        }

        if (constraint.Max.HasValue)
        {
            writer.WriteNumber("max", constraint.Max.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteOperationResultContract (
        Utf8JsonWriter writer,
        UcliOperationResultContract? resultContract)
    {
        writer.WritePropertyName("resultContract");
        if (resultContract == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteBoolean("emitted", resultContract.Emitted);
        WriteNullableString(writer, "resultType", resultContract.ResultType);
        WriteNullableString(writer, "description", resultContract.Description);
        writer.WriteEndObject();
    }

    private static void WriteOperationAssurance (
        Utf8JsonWriter writer,
        UcliOperationAssuranceContract? assurance)
    {
        writer.WritePropertyName("assurance");
        if (assurance == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteStringArray(writer, "sideEffects", assurance.SideEffects);
        writer.WriteBoolean("mayDirty", assurance.MayDirty);
        writer.WriteBoolean("mayPersist", assurance.MayPersist);
        WriteStringArray(writer, "touchedKinds", assurance.TouchedKinds);
        WriteNullableString(writer, "planMode", assurance.PlanMode);
        WriteNullableString(writer, "planSemantics", assurance.PlanSemantics);
        WriteNullableString(writer, "callSemantics", assurance.CallSemantics);
        WriteNullableString(writer, "touchedContract", assurance.TouchedContract);
        WriteNullableString(writer, "readPostconditionContract", assurance.ReadPostconditionContract);
        WriteNullableString(writer, "failureSemantics", assurance.FailureSemantics);
        WriteStringArray(writer, "dangerousNotes", assurance.DangerousNotes);
        writer.WriteEndObject();
    }

    private static void WriteSchema (
        Utf8JsonWriter writer,
        string propertyName,
        string? schemaJson)
    {
        writer.WritePropertyName(propertyName);
        if (schemaJson == null)
        {
            writer.WriteNullValue();
            return;
        }

        using var document = JsonDocument.Parse(schemaJson);
        document.RootElement.WriteTo(writer);
    }

    private static void WriteOptionalSchema (
        Utf8JsonWriter writer,
        string propertyName,
        string? schemaJson)
    {
        if (schemaJson == null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        WriteSchema(writer, propertyName, schemaJson);
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
        WriteNullableString(writer, "language", codeContract.Language);
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
        WriteNullableString(writer, "kind", sourceForm.Kind);
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
        WriteNullableString(writer, "kind", member.Kind);
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
