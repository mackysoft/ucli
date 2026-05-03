using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Prefab creation operation arguments.")]
public sealed record PrefabCreateArgs
{
    [JsonConstructor]
    public PrefabCreateArgs (
        SceneGameObjectReferenceArgs target,
        string path)
    {
        Target = target;
        Path = path;
    }

    [UcliRequired]
    [UcliDescription("Source scene GameObject reference.")]
    public SceneGameObjectReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Prefab asset path to create.")]
    [UcliMinLength(1)]
    public string Path { get; init; }
}
