namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one asset-search lookup read output. </summary>
internal sealed record AssetSearchLookupReadOutput (
    IReadOnlyList<IndexAssetSearchEntryJsonContract> Entries,
    AssetLookupAccessInfo AccessInfo);
