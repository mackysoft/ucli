using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Prefab path operation arguments.")]
public sealed record PrefabPathArgs
{
    [JsonConstructor]
    public PrefabPathArgs (PrefabAssetPath path)
    {
        Path = path;
    }

    public PrefabPathArgs (string path)
        : this(new PrefabAssetPath(path))
    {
    }

    [UcliRequired]
    public PrefabAssetPath Path { get; init; }
}
