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
        writer.WriteNumber("schemaVersion", contract.SchemaVersion);
        writer.WriteString("generatedAtUtc", contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteOperationEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexOpEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexOpEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderOpsEntries(entries);
    }
}
