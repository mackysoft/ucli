namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one operation-catalog snapshot projected for <c>ops list</c>. </summary>
internal sealed record OpsCatalogListSnapshot
{
    internal OpsCatalogListSnapshot (OpsCatalogListEntry[] operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        Operations = Array.AsReadOnly(operations.ToArray());
    }

    /// <summary> Gets the list projection entries. </summary>
    public IReadOnlyList<OpsCatalogListEntry> Operations { get; }
}
