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
        writer.WriteNumber("schemaVersion", contract.SchemaVersion);
        writer.WriteString("generatedAtUtc", contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteTypeEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexTypeEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexTypeEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderTypeEntries(entries);
    }
}
