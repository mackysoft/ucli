using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Provides shared write helpers for persisted read-index JSON contracts. </summary>
/// <typeparam name="TContract"> The read-index contract type. </typeparam>
internal abstract class IndexJsonContractWriterBase<TContract> : JsonContractWriter<TContract>
{
    protected static void WriteTypeEntry (
        Utf8JsonWriter writer,
        IndexTypeEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "typeId", entry.TypeId);
        WriteNullableString(writer, "displayName", entry.DisplayName);
        WriteNullableString(writer, "namespace", entry.Namespace);
        WriteNullableString(writer, "assemblyName", entry.AssemblyName);
        WriteNullableString(writer, "baseTypeId", entry.BaseTypeId);
        writer.WritePropertyName("flags");
        WriteTypeFlags(writer, entry.Flags);
        writer.WriteEndObject();
    }

    protected static void WriteSchemaEntry (
        Utf8JsonWriter writer,
        IndexSchemaEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "schemaKey", entry.SchemaKey);
        WriteNullableString(writer, "kind", entry.Kind);
        WriteNullableString(writer, "typeId", entry.TypeId);
        WriteNullableString(writer, "displayName", entry.DisplayName);
        WriteArray(writer, "properties", OrderSchemaPropertiesOrNull(entry.Properties), WriteSchemaProperty);
        writer.WriteEndObject();
    }

    protected static void WriteOperationEntry (
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
        WriteOperationInputs(writer, entry.Inputs);
        WriteOperationResultContract(writer, entry.ResultContract);
        WriteOperationAssurance(writer, entry.Assurance);
        writer.WriteEndObject();
    }

    protected static void WriteAssetSearchEntry (
        Utf8JsonWriter writer,
        IndexAssetSearchEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "assetPath", entry.AssetPath);
        WriteNullableString(writer, "assetGuid", entry.AssetGuid);
        WriteNullableString(writer, "name", entry.Name);
        WriteNullableString(writer, "typeId", entry.TypeId);
        WriteStringArray(writer, "searchTypeIds", entry.SearchTypeIds);
        writer.WriteEndObject();
    }

    protected static void WriteGuidPathEntry (
        Utf8JsonWriter writer,
        IndexGuidPathEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "assetGuid", entry.AssetGuid);
        WriteNullableString(writer, "assetPath", entry.AssetPath);
        writer.WriteEndObject();
    }

    protected static void WriteSceneTreeLiteNode (
        Utf8JsonWriter writer,
        IndexSceneTreeLiteNodeJsonContract node)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", node.Name);
        WriteNullableString(writer, "globalObjectId", node.GlobalObjectId);
        WriteArray(writer, "children", node.Children, WriteSceneTreeLiteNode);
        writer.WriteEndObject();
    }

    protected static void WriteArray<TItem> (
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

    private static void WriteTypeFlags (
        Utf8JsonWriter writer,
        IndexTypeFlagsJsonContract? flags)
    {
        if (flags == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteBoolean("isAbstract", flags.IsAbstract);
        writer.WriteBoolean("isGenericDefinition", flags.IsGenericDefinition);
        writer.WriteBoolean("isUnityObject", flags.IsUnityObject);
        writer.WriteBoolean("isComponent", flags.IsComponent);
        writer.WriteBoolean("isScriptableObject", flags.IsScriptableObject);
        writer.WriteBoolean("isSerializeReferenceCandidate", flags.IsSerializeReferenceCandidate);
        writer.WriteEndObject();
    }

    private static void WriteSchemaProperty (
        Utf8JsonWriter writer,
        IndexSchemaPropertyEntryJsonContract property)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "path", property.Path);
        WriteNullableString(writer, "propertyType", property.PropertyType);
        WriteNullableString(writer, "declaredTypeId", property.DeclaredTypeId);
        writer.WriteBoolean("isArray", property.IsArray);
        WriteNullableString(writer, "elementTypeId", property.ElementTypeId);
        writer.WriteBoolean("isReadOnly", property.IsReadOnly);
        writer.WriteEndObject();
    }

    private static void WriteOperationInputs (
        Utf8JsonWriter writer,
        IReadOnlyList<UcliOperationInputContract>? inputs)
    {
        WriteArray(writer, "inputs", inputs, WriteOperationInput);
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

    private static IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? OrderSchemaPropertiesOrNull (IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? properties)
    {
        return properties == null ? null : IndexJsonOrderingPolicy.OrderSchemaProperties(properties);
    }
}
