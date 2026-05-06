using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>guid-path.lookup.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexGuidPathLookupJsonContractWriter : IndexJsonContractWriterBase<IndexGuidPathLookupJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexGuidPathLookupJsonContract contract)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", contract.SchemaVersion);
        writer.WriteString("generatedAtUtc", contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteGuidPathEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexGuidPathEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexGuidPathEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderGuidPathEntries(entries);
    }
}
