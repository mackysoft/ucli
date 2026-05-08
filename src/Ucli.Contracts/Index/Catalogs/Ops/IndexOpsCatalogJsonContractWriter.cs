using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>ops.catalog.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexOpsCatalogJsonContractWriter : IndexJsonContractWriterBase<IndexOpsCatalogJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexOpsCatalogJsonContract contract)
    {
        writer.WriteStartObject();
        WriteRootHeader(writer, contract.SchemaVersion, contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteOperationEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexOpEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexOpEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderOpsEntries(entries);
    }

    private static void WriteOperationEntry (
        Utf8JsonWriter writer,
        IndexOpEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", entry.Name);
        WriteNullableString(writer, "kind", entry.Kind);
        WriteNullableString(writer, "policy", entry.Policy);
        WriteNullableString(writer, "argsSchemaJson", entry.ArgsSchemaJson);
        if (entry.ResultSchemaJson != null)
        {
            writer.WriteString("resultSchemaJson", entry.ResultSchemaJson);
        }

        WriteNullableString(writer, "description", entry.Description);
        WriteArray(writer, "inputs", entry.Inputs, WriteOperationInput);
        WriteOperationResultContract(writer, entry.ResultContract);
        WriteOperationAssurance(writer, entry.Assurance);
        WriteOperationCodeContract(writer, entry.CodeContract);
        writer.WriteEndObject();
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
        writer.WriteEndObject();
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
        writer.WriteBoolean("requiredStatic", entryPoint.RequiredStatic);
        WriteStringArray(writer, "parameterTypes", entryPoint.ParameterTypes);
        WriteNullableString(writer, "returnValue", entryPoint.ReturnValue);
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
        if (member.Type != null)
        {
            writer.WriteString("type", member.Type);
        }

        if (member.ReturnType != null)
        {
            writer.WriteString("returnType", member.ReturnType);
        }

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
