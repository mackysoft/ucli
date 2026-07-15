using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Execution.OperationMetadata;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Creates <c>ops list</c> read snapshots from validated operation-catalog data. </summary>
internal static class OpsCatalogListSnapshotFactory
{
    /// <summary> Creates a list snapshot from a lightweight descriptor snapshot. </summary>
    public static OpsCatalogListSnapshot FromDescriptors (OpsCatalogDescriptorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new OpsCatalogListSnapshot(
            snapshot.Entries
                .Where(static entry => !EditLoweringOnlyPrimitiveOperationNames.Contains(entry.Name))
                .Select(static entry => new OpsCatalogListEntry(
                    entry.Name,
                    entry.Kind,
                    entry.Policy,
                    entry.Description))
                .ToArray());
    }

    /// <summary> Creates a list snapshot from a full operation-catalog snapshot. </summary>
    public static OpsCatalogListSnapshot FromCatalog (OpsCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new OpsCatalogListSnapshot(
            snapshot.Operations
                .Where(static operation => !EditLoweringOnlyPrimitiveOperationNames.Contains(operation.Name))
                .Select(static operation => new OpsCatalogListEntry(
                    operation.Name,
                    operation.Kind,
                    operation.Policy,
                    operation.Description))
                .ToArray());
    }
}
