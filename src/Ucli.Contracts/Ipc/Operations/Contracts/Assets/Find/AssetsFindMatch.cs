using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single asset search match.")]
public sealed record AssetsFindMatch
{
    [JsonConstructor]
    public AssetsFindMatch (
        UnityAssetPath assetPath,
        UnityAssetGuid assetGuid,
        string name,
        UnityTypeId typeId)
    {
        AssetPath = assetPath;
        AssetGuid = assetGuid;
        Name = name;
        TypeId = typeId;
    }

    [UcliRequired]
    [UcliDescription("Unity project relative asset path.")]
    public UnityAssetPath AssetPath { get; init; }

    [UcliRequired]
    [UcliDescription("Unity asset GUID.")]
    public UnityAssetGuid AssetGuid { get; init; }

    [UcliRequired]
    [UcliDescription("Asset object name.")]
    public string Name { get; init; }

    [UcliRequired]
    [UcliDescription("Asset type identifier.")]
    public UnityTypeId TypeId { get; init; }
}
