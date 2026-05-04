using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene path operation arguments.")]
public sealed record ScenePathArgs
{
    [JsonConstructor]
    public ScenePathArgs (SceneAssetPath path)
    {
        Path = path;
    }

    public ScenePathArgs (string path)
        : this(new SceneAssetPath(path))
    {
    }

    [UcliRequired]
    public SceneAssetPath Path { get; init; }
}
