using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Applies v1 public raw-catalog inclusion rules to validated operation entries. </summary>
internal static class PublicRawOperationCatalogFilter
{
    /// <summary> Gets a value indicating whether an operation is included in the public raw catalog. </summary>
    public static bool Includes (IndexOpEntryJsonContract operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return !UcliOperationPublicCatalogRules.HasPublicRawCatalogExclusionMarker(operation.Assurance?.SideEffects);
    }

    /// <summary> Filters operation entries while preserving input order. </summary>
    public static IReadOnlyList<IndexOpEntryJsonContract> Filter (IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        List<IndexOpEntryJsonContract>? includedOperations = null;
        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            if (operation == null)
            {
                throw new InvalidOperationException("Operation catalog contains a null entry.");
            }

            if (Includes(operation))
            {
                includedOperations?.Add(operation);
                continue;
            }

            includedOperations ??= CopyPrefix(operations, i);
        }

        return includedOperations?.ToArray() ?? operations;
    }

    private static List<IndexOpEntryJsonContract> CopyPrefix (
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        int exclusiveEnd)
    {
        var copied = new List<IndexOpEntryJsonContract>(operations.Count);
        for (var i = 0; i < exclusiveEnd; i++)
        {
            copied.Add(operations[i]);
        }

        return copied;
    }
}
