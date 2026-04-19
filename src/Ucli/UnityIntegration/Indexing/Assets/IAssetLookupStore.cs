using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Persists asset lookup artifacts to the local read-index store. </summary>
internal interface IAssetLookupStore
{
    /// <summary> Writes asset-search and GUID-path lookup artifacts for one project fingerprint. </summary>
    ValueTask Write (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        IndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default);
}