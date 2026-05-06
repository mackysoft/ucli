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
}
