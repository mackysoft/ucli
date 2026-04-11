using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Assets.Access;

/// <summary> Represents one asset-search lookup read output. </summary>
internal sealed record AssetSearchLookupReadOutput (
    IReadOnlyList<IndexAssetSearchEntryJsonContract> Entries,
    AssetLookupAccessInfo AccessInfo);