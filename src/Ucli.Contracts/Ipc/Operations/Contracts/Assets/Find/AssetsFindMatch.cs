using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single asset search match.")]
public sealed record AssetsFindMatch
{
    [JsonConstructor]
    public AssetsFindMatch (
        UnityAssetPath assetPath,
        UnityAssetGuid? assetGuid,
        string name,
        UnityTypeId typeId)
    {
        AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
        AssetGuid = assetGuid;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Asset name must not be empty or whitespace.", nameof(name));
        }

        Name = name;
        TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
    }

    [UcliRequired]
    [UcliDescription("Unity project relative asset path.")]
    public UnityAssetPath AssetPath { get; }

    [UcliDescription("Unity asset GUID, or null when a planned asset has not been imported yet.")]
    public UnityAssetGuid? AssetGuid { get; }

    [UcliRequired]
    [UcliDescription("Asset object name.")]
    public string Name { get; }

    [UcliRequired]
    [UcliDescription("Asset type identifier.")]
    public UnityTypeId TypeId { get; }
}
