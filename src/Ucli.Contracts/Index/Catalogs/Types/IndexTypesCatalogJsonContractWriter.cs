using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>types.catalog.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexTypesCatalogJsonContractWriter : IndexJsonContractWriterBase<IndexTypesCatalogJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexTypesCatalogJsonContract contract)
    {
        writer.WriteStartObject();
        WriteRootHeader(writer, contract.SchemaVersion, contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteTypeEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexTypeEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexTypeEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderTypeEntries(entries);
    }

    private static void WriteTypeEntry (
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
}
