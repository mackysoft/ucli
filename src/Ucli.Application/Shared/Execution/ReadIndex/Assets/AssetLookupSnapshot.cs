using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one validated live asset lookup snapshot. </summary>
internal sealed record AssetLookupSnapshot
{
    private AssetLookupSnapshot (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<AssetSearchLookupEntry> assetSearchEntries,
        IReadOnlyList<GuidPathLookupEntry> guidPathEntries)
    {
        GeneratedAtUtc = generatedAtUtc;
        AssetSearchEntries = Array.AsReadOnly(assetSearchEntries.ToArray());
        GuidPathEntries = Array.AsReadOnly(guidPathEntries.ToArray());
    }

    /// <summary> Gets the snapshot generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the asset-search entries in ordinal asset-path order. </summary>
    public IReadOnlyList<AssetSearchLookupEntry> AssetSearchEntries { get; }

    /// <summary> Gets the GUID-path entries in ordinal asset-path order. </summary>
    public IReadOnlyList<GuidPathLookupEntry> GuidPathEntries { get; }

    /// <summary> Creates a snapshot when both lookup sets are sorted and mutually consistent. </summary>
    public static bool TryCreate (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<AssetSearchLookupEntry>? assetSearchEntries,
        IReadOnlyList<GuidPathLookupEntry>? guidPathEntries,
        [NotNullWhen(true)]
        out AssetLookupSnapshot? snapshot,
        out string? error)
    {
        snapshot = null;
        if (assetSearchEntries == null || guidPathEntries == null)
        {
            error = "Lookup entry collections are required.";
            return false;
        }

        if (generatedAtUtc == default || generatedAtUtc.Offset != TimeSpan.Zero)
        {
            error = "Lookup generation timestamp must be a non-default UTC value.";
            return false;
        }

        if (assetSearchEntries.Any(static entry => entry is null)
            || guidPathEntries.Any(static entry => entry is null))
        {
            error = "Lookup entry collections must not contain null values.";
            return false;
        }

        if (!AreSortedByAssetPath(assetSearchEntries)
            || !AreSortedByAssetPath(guidPathEntries))
        {
            error = "Lookup entries must be sorted by assetPath.";
            return false;
        }

        if (!GuidPathEntriesMatchAssetSearchEntries(assetSearchEntries, guidPathEntries))
        {
            error = "guidPathEntries must be represented in assetSearchEntries.";
            return false;
        }

        snapshot = new AssetLookupSnapshot(generatedAtUtc, assetSearchEntries, guidPathEntries);
        error = null;
        return true;
    }

    private static bool AreSortedByAssetPath (IReadOnlyList<AssetSearchLookupEntry> entries)
    {
        for (var i = 1; i < entries.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(entries[i - 1].AssetPath.Value, entries[i].AssetPath.Value) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSortedByAssetPath (IReadOnlyList<GuidPathLookupEntry> entries)
    {
        for (var i = 1; i < entries.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(entries[i - 1].AssetPath.Value, entries[i].AssetPath.Value) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool GuidPathEntriesMatchAssetSearchEntries (
        IReadOnlyList<AssetSearchLookupEntry> assetSearchEntries,
        IReadOnlyList<GuidPathLookupEntry> guidPathEntries)
    {
        var assetSearchIndex = 0;
        for (var i = 0; i < guidPathEntries.Count; i++)
        {
            var guidPathEntry = guidPathEntries[i];
            while (assetSearchIndex < assetSearchEntries.Count
                && StringComparer.Ordinal.Compare(
                    assetSearchEntries[assetSearchIndex].AssetPath.Value,
                    guidPathEntry.AssetPath.Value) < 0)
            {
                assetSearchIndex++;
            }

            if (assetSearchIndex >= assetSearchEntries.Count)
            {
                return false;
            }

            var assetSearchEntry = assetSearchEntries[assetSearchIndex];
            if (assetSearchEntry.AssetPath != guidPathEntry.AssetPath
                || assetSearchEntry.AssetGuid != guidPathEntry.AssetGuid)
            {
                return false;
            }

            assetSearchIndex++;
        }

        return true;
    }
}
