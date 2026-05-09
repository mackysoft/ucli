using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Creates <c>ops list</c> read snapshots from validated operation-catalog data. </summary>
internal static class OpsCatalogListSnapshotFactory
{
    /// <summary> Creates a list snapshot from a lightweight descriptor snapshot. </summary>
    public static OpsCatalogListSnapshot FromDescriptors (OpsCatalogDescriptorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new OpsCatalogListSnapshot(
            snapshot.Entries.Select(static entry => CreateEntry(
                    entry.Name,
                    entry.Kind,
                    entry.Policy))
                .ToArray());
    }

    /// <summary> Creates a list snapshot from a full operation-catalog snapshot. </summary>
    public static OpsCatalogListSnapshot FromCatalog (OpsCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new OpsCatalogListSnapshot(
            snapshot.Operations.Select(static operation => CreateEntry(
                    operation.Name,
                    operation.Kind,
                    operation.Policy))
                .ToArray());
    }

    private static OpsCatalogListEntry CreateEntry (
        string? name,
        string? kind,
        string? policy)
    {
        return new OpsCatalogListEntry(
            name!,
            ParseKind(kind),
            ParsePolicy(policy));
    }

    private static UcliOperationKind ParseKind (string? value)
    {
        if (!UcliOperationKindCodec.TryParse(value, out var kind))
        {
            throw new InvalidOperationException($"Operation kind is invalid: {value}");
        }

        return kind;
    }

    private static OperationPolicy ParsePolicy (string? value)
    {
        if (!OperationPolicyCodec.TryParse(value, out var policy))
        {
            throw new InvalidOperationException($"Operation policy is invalid: {value}");
        }

        return policy;
    }
}
