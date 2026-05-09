namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one operation-catalog snapshot projected for <c>ops list</c>. </summary>
internal sealed record OpsCatalogListSnapshot
{
    private OpsCatalogListSnapshot (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<OpsCatalogListEntry> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        GeneratedAtUtc = generatedAtUtc;
        Operations = operations.ToArray();
    }

    /// <summary> Gets the catalog generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the list projection entries. </summary>
    public IReadOnlyList<OpsCatalogListEntry> Operations { get; }

    /// <summary> Creates a list snapshot from a lightweight descriptor snapshot. </summary>
    public static OpsCatalogListSnapshot FromDescriptors (OpsCatalogDescriptorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new OpsCatalogListSnapshot(
            snapshot.GeneratedAtUtc,
            snapshot.Entries.Select(static entry => new OpsCatalogListEntry(
                    entry.Name!,
                    entry.Kind!,
                    entry.Policy!))
                .ToArray());
    }

    /// <summary> Creates a list snapshot from a full operation-catalog snapshot. </summary>
    public static OpsCatalogListSnapshot FromCatalog (OpsCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new OpsCatalogListSnapshot(
            snapshot.GeneratedAtUtc,
            snapshot.Operations.Select(static operation => new OpsCatalogListEntry(
                    operation.Name!,
                    operation.Kind!,
                    operation.Policy!))
                .ToArray());
    }
}
