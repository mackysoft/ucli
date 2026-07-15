using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single asset search match.")]
public sealed record AssetsFindMatch
{
    /// <exception cref="ArgumentException"> Thrown when <paramref name="assetGuid" /> is <see cref="Guid.Empty" /> or <paramref name="name" /> is empty or whitespace. </exception>
    [JsonConstructor]
    public AssetsFindMatch (
        UnityAssetPath assetPath,
        Guid? assetGuid,
        string name,
        UnityTypeId typeId)
    {
        AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Asset GUID must not be empty.", nameof(assetGuid));
        }

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
    public Guid? AssetGuid { get; }

    [UcliRequired]
    [UcliDescription("Asset object name.")]
    public string Name { get; }

    [UcliRequired]
    [UcliDescription("Asset type identifier.")]
    public UnityTypeId TypeId { get; }
}
