using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Prefab creation operation arguments.")]
public sealed record PrefabCreateArgs
{
    [JsonConstructor]
    public PrefabCreateArgs (
        SceneGameObjectReferenceArgs target,
        CreatablePrefabAssetPath path)
    {
        Target = target;
        Path = path;
    }

    public PrefabCreateArgs (
        SceneGameObjectReferenceArgs target,
        string path)
        : this(target, new CreatablePrefabAssetPath(path))
    {
    }

    [UcliRequired]
    [UcliDescription("Source scene GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public SceneGameObjectReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Prefab asset path to create.")]
    public CreatablePrefabAssetPath Path { get; init; }
}
