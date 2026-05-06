using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>asset-search.lookup.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexAssetSearchLookupJsonContractWriter : IndexJsonContractWriterBase<IndexAssetSearchLookupJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexAssetSearchLookupJsonContract contract)
    {
        writer.WriteStartObject();
        WriteRootHeader(writer, contract.SchemaVersion, contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        WriteArray(writer, "entries", OrderEntriesOrNull(contract.Entries), WriteAssetSearchEntry);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<IndexAssetSearchEntryJsonContract>? OrderEntriesOrNull (IReadOnlyList<IndexAssetSearchEntryJsonContract>? entries)
    {
        return entries == null ? null : IndexJsonOrderingPolicy.OrderAssetSearchEntries(entries);
    }

    private static void WriteAssetSearchEntry (
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
}
