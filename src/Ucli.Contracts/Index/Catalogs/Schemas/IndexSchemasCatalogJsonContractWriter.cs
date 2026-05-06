using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>schemas.catalog.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexSchemasCatalogJsonContractWriter : IndexJsonContractWriterBase<IndexSchemasCatalogJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexSchemasCatalogJsonContract contract)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", contract.SchemaVersion);
        writer.WriteString("generatedAtUtc", contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteSchemaEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexSchemaEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexSchemaEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderSchemaEntries(entries);
    }

    private static void WriteSchemaEntry (
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

    private static IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? OrderSchemaPropertiesOrNull (IReadOnlyList<IndexSchemaPropertyEntryJsonContract>? properties)
    {
        return properties == null ? null : IndexJsonOrderingPolicy.OrderSchemaProperties(properties);
    }
}
