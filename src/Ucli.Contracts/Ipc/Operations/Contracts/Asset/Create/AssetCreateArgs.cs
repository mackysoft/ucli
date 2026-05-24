using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset creation operation arguments.")]
public sealed record AssetCreateArgs
{
    [JsonConstructor]
    public AssetCreateArgs (
        UnityTypeId type,
        CreatableUnityAssetPath path)
    {
        Type = type;
        Path = path;
    }

    public AssetCreateArgs (
        string type,
        string path)
        : this(new UnityTypeId(type), new CreatableUnityAssetPath(path))
    {
    }

    [UcliRequired]
    [UcliDescription("Unity asset type identifier to create.")]
    public UnityTypeId Type { get; init; }

    [UcliRequired]
    [UcliDescription("Unity project relative asset path to create.")]
    public CreatableUnityAssetPath Path { get; init; }
}
