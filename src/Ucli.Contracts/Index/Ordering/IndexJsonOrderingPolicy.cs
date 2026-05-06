namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines deterministic ordering for index JSON contracts and matching generated payloads. </summary>
internal static class IndexJsonOrderingPolicy
{
    /// <summary> Orders operation entries by operation name. </summary>
    public static IReadOnlyList<IndexOpEntryJsonContract> OrderOpsEntries (IEnumerable<IndexOpEntryJsonContract> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        return entries
            .OrderBy(static entry => entry.Name ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Orders type entries by type identifier. </summary>
    public static IReadOnlyList<IndexTypeEntryJsonContract> OrderTypeEntries (IEnumerable<IndexTypeEntryJsonContract> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        return entries
            .OrderBy(static entry => entry.TypeId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Orders schema entries by schema key. </summary>
    public static IReadOnlyList<IndexSchemaEntryJsonContract> OrderSchemaEntries (IEnumerable<IndexSchemaEntryJsonContract> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        return entries
            .OrderBy(static entry => entry.SchemaKey ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Orders schema property entries by serialized property path. </summary>
    public static IReadOnlyList<IndexSchemaPropertyEntryJsonContract> OrderSchemaProperties (IEnumerable<IndexSchemaPropertyEntryJsonContract> properties)
    {
        if (properties == null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        return properties
            .OrderBy(static property => property.Path ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Orders asset-search lookup entries by asset path. </summary>
    public static IReadOnlyList<IndexAssetSearchEntryJsonContract> OrderAssetSearchEntries (IEnumerable<IndexAssetSearchEntryJsonContract> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        return entries
            .OrderBy(static entry => entry.AssetPath ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Orders GUID-path lookup entries by asset path. </summary>
    public static IReadOnlyList<IndexGuidPathEntryJsonContract> OrderGuidPathEntries (IEnumerable<IndexGuidPathEntryJsonContract> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        return entries
            .OrderBy(static entry => entry.AssetPath ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }
}
