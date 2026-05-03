using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset creation operation arguments.")]
public sealed record AssetCreateArgs
{
    [JsonConstructor]
    public AssetCreateArgs (
        string type,
        string path)
    {
        Type = type;
        Path = path;
    }

    [UcliRequired]
    [UcliDescription("Unity asset type identifier to create.")]
    public string Type { get; init; }

    [UcliRequired]
    [UcliDescription("Unity project relative asset path to create.")]
    public string Path { get; init; }
}
