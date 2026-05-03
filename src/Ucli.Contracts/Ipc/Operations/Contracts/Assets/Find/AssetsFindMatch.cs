using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single asset search match.")]
public sealed record AssetsFindMatch
{
    [JsonConstructor]
    public AssetsFindMatch (
        string assetPath,
        string assetGuid,
        string name,
        string typeId)
    {
        AssetPath = assetPath;
        AssetGuid = assetGuid;
        Name = name;
        TypeId = typeId;
    }

    [UcliRequired]
    [UcliDescription("Unity project relative asset path.")]
    public string AssetPath { get; init; }

    [UcliRequired]
    [UcliDescription("Unity asset GUID.")]
    public string AssetGuid { get; init; }

    [UcliRequired]
    [UcliDescription("Asset object name.")]
    public string Name { get; init; }

    [UcliRequired]
    [UcliDescription("Asset type identifier.")]
    public string TypeId { get; init; }
}
