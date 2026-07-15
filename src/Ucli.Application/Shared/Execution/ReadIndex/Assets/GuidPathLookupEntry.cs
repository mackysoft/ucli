using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one validated asset GUID-to-path entry inside the application boundary. </summary>
internal sealed record GuidPathLookupEntry
{
    /// <summary> Initializes one validated GUID-to-path entry. </summary>
    public GuidPathLookupEntry (
        Guid assetGuid,
        UnityAssetPath assetPath)
    {
        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Asset GUID must not be empty.", nameof(assetGuid));
        }

        AssetGuid = assetGuid;
        AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
    }

    /// <summary> Gets the non-empty asset GUID. </summary>
    public Guid AssetGuid { get; }

    /// <summary> Gets the normalized Unity asset path. </summary>
    public UnityAssetPath AssetPath { get; }
}
