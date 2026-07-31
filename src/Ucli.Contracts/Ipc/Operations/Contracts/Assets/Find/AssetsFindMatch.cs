using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Single asset search match.")]
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

    [JsonInclude]
    [JsonRequired]
    [Description("Unity project relative asset path.")]
    public UnityAssetPath AssetPath { get; private init; }

    [Description("Unity asset GUID, or null when a planned asset has not been imported yet.")]
    public Guid? AssetGuid { get; }

    [JsonInclude]
    [JsonRequired]
    [Description("Asset object name.")]
    public string Name { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Asset type identifier.")]
    public UnityTypeId TypeId { get; private init; }
}
