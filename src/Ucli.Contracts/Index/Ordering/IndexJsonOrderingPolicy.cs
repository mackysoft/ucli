namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines deterministic ordering for index JSON contracts and matching generated payloads. </summary>
internal static class IndexJsonOrderingPolicy
{
    /// <summary> Orders operation entries by operation name. </summary>
    public static IReadOnlyList<IndexOpEntryJsonContract> OrderOpsEntries (IEnumerable<IndexOpEntryJsonContract> entries)
    {
        return OrderByOrdinalKey(entries, static entry => entry.Name);
    }

    /// <summary> Orders lightweight operation catalog entries by operation name. </summary>
    public static IReadOnlyList<IndexOpsCatalogEntryJsonContract> OrderOpsCatalogEntries (IEnumerable<IndexOpsCatalogEntryJsonContract> entries)
    {
        return OrderByOrdinalKey(entries, static entry => entry.Name);
    }

    /// <summary> Orders type entries by type identifier. </summary>
    public static IReadOnlyList<IndexTypeEntryJsonContract> OrderTypeEntries (IEnumerable<IndexTypeEntryJsonContract> entries)
    {
        return OrderByOrdinalKey(entries, static entry => entry.TypeId);
    }

    /// <summary> Orders schema entries by schema key. </summary>
    public static IReadOnlyList<IndexSchemaEntryJsonContract> OrderSchemaEntries (IEnumerable<IndexSchemaEntryJsonContract> entries)
    {
        return OrderByOrdinalKey(entries, static entry => entry.SchemaKey);
    }

    /// <summary> Orders schema property entries by serialized property path. </summary>
    public static IReadOnlyList<IndexSchemaPropertyEntryJsonContract> OrderSchemaProperties (IEnumerable<IndexSchemaPropertyEntryJsonContract> properties)
    {
        return OrderByOrdinalKey(properties, static property => property.Path);
    }

    /// <summary> Orders asset-search lookup entries by asset path. </summary>
    public static IReadOnlyList<IndexAssetSearchEntryJsonContract> OrderAssetSearchEntries (IEnumerable<IndexAssetSearchEntryJsonContract> entries)
    {
        return OrderByOrdinalKey(entries, static entry => entry.AssetPath);
    }

    /// <summary> Orders GUID-path lookup entries by asset path. </summary>
    public static IReadOnlyList<IndexGuidPathEntryJsonContract> OrderGuidPathEntries (IEnumerable<IndexGuidPathEntryJsonContract> entries)
    {
        return OrderByOrdinalKey(entries, static entry => entry.AssetPath);
    }

    private static IReadOnlyList<TEntry> OrderByOrdinalKey<TEntry> (
        IEnumerable<TEntry> entries,
        Func<TEntry, string?> keySelector)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        return entries
            .OrderBy(entry => keySelector(entry) ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }
}
