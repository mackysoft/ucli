using System.Text.Json;

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
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteCatalogEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexOpsCatalogEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexOpsCatalogEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderOpsCatalogEntries(entries);
    }

    private static void WriteCatalogEntry (
        Utf8JsonWriter writer,
        IndexOpsCatalogEntryJsonContract entry)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "name", entry.Name);
        WriteNullableString(writer, "kind", entry.Kind);
        WriteNullableString(writer, "policy", entry.Policy);
        WriteNullableString(writer, "describeKey", entry.DescribeKey);
        WriteNullableString(writer, "describeHash", entry.DescribeHash);
        writer.WriteEndObject();
    }
}
