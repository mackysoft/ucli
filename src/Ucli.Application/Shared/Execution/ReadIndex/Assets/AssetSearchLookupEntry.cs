using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one validated asset-search entry used inside the application boundary. </summary>
internal sealed record AssetSearchLookupEntry
{
    /// <summary> Initializes one validated asset-search entry. </summary>
    public AssetSearchLookupEntry (
        UnityAssetPath assetPath,
        Guid? assetGuid,
        string name,
        UnityTypeId typeId,
        IReadOnlyList<UnityTypeId> searchTypeIds)
    {
        AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Asset GUID must be non-empty when specified.", nameof(assetGuid));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(typeId);
        ArgumentNullException.ThrowIfNull(searchTypeIds);
        if (searchTypeIds.Count == 0)
        {
            throw new ArgumentException("At least one search type identifier is required.", nameof(searchTypeIds));
        }

        var copiedSearchTypeIds = new UnityTypeId[searchTypeIds.Count];
        for (var i = 0; i < searchTypeIds.Count; i++)
        {
            var searchTypeId = searchTypeIds[i];
            copiedSearchTypeIds[i] = searchTypeId
                ?? throw new ArgumentException("Search type identifiers must not contain null.", nameof(searchTypeIds));
        }

        AssetGuid = assetGuid;
        Name = name;
        TypeId = typeId;
        SearchTypeIds = Array.AsReadOnly(copiedSearchTypeIds);
    }

    /// <summary> Gets the normalized Unity asset path. </summary>
    public UnityAssetPath AssetPath { get; }

    /// <summary> Gets the imported asset GUID, or <see langword="null" /> when Unity has not assigned one. </summary>
    public Guid? AssetGuid { get; }

    /// <summary> Gets the asset name. </summary>
    public string Name { get; }

    /// <summary> Gets the concrete type identifier. </summary>
    public UnityTypeId TypeId { get; }

    /// <summary> Gets the validated search type identifiers. </summary>
    public IReadOnlyList<UnityTypeId> SearchTypeIds { get; }

    /// <summary> Determines whether the entry is assignable to the specified type identifier. </summary>
    public bool ContainsSearchTypeId (UnityTypeId typeId)
    {
        ArgumentNullException.ThrowIfNull(typeId);
        for (var i = 0; i < SearchTypeIds.Count; i++)
        {
            if (SearchTypeIds[i] == typeId)
            {
                return true;
            }
        }

        return false;
    }
}
