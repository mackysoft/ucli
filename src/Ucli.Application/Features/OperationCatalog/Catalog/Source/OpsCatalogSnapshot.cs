namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one validated operation-catalog snapshot. </summary>
internal sealed record OpsCatalogSnapshot
{
    private OpsCatalogSnapshot (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        GeneratedAtUtc = generatedAtUtc;
        Operations = PublicRawOperationCatalogFilter.Filter(operations).ToArray();
    }

    /// <summary> Gets the catalog generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the validated operation entries. </summary>
    public IReadOnlyList<IndexOpEntryJsonContract> Operations { get; }

    /// <summary> Creates a snapshot when the entry collection satisfies the public operation-entry contract. </summary>
    public static bool TryCreate (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract>? operations,
        string propertyName,
        out OpsCatalogSnapshot? snapshot,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (!IndexCatalogContractValidator.TryValidateOpsEntries(operations, propertyName, out error))
        {
            snapshot = null;
            return false;
        }

        snapshot = new OpsCatalogSnapshot(generatedAtUtc, operations!);
        error = null;
        return true;
    }
}
