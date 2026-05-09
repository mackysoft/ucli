namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one validated lightweight operation-catalog descriptor snapshot. </summary>
internal sealed record OpsCatalogDescriptorSnapshot
{
    private OpsCatalogDescriptorSnapshot (
        DateTimeOffset generatedAtUtc,
        string sourceInputsHash,
        IReadOnlyList<IndexOpsCatalogEntryJsonContract> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInputsHash);
        ArgumentNullException.ThrowIfNull(entries);

        GeneratedAtUtc = generatedAtUtc;
        SourceInputsHash = sourceInputsHash;
        Entries = entries.ToArray();
    }

    /// <summary> Gets the catalog generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the source-inputs hash shared by all describe artifacts. </summary>
    public string SourceInputsHash { get; }

    /// <summary> Gets the validated lightweight operation descriptor entries. </summary>
    public IReadOnlyList<IndexOpsCatalogEntryJsonContract> Entries { get; }

    /// <summary> Creates a descriptor snapshot when the entry collection satisfies the public catalog contract. </summary>
    public static bool TryCreate (
        DateTimeOffset generatedAtUtc,
        string? sourceInputsHash,
        IReadOnlyList<IndexOpsCatalogEntryJsonContract>? entries,
        string propertyName,
        out OpsCatalogDescriptorSnapshot? snapshot,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (string.IsNullOrWhiteSpace(sourceInputsHash))
        {
            snapshot = null;
            error = "sourceInputsHash is missing.";
            return false;
        }

        if (!IndexCatalogContractValidator.TryValidateOpsCatalogEntries(entries, propertyName, out error))
        {
            snapshot = null;
            return false;
        }

        snapshot = new OpsCatalogDescriptorSnapshot(generatedAtUtc, sourceInputsHash, entries!);
        error = null;
        return true;
    }
}
