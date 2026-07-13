using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Prefab creation operation arguments.")]
public sealed record PrefabCreateArgs
{
    [JsonConstructor]
    public PrefabCreateArgs (
        SceneGameObjectReferenceArgs target,
        PrefabAssetPath path)
    {
        Target = target;
        Path = path;
    }

    [UcliRequired]
    [UcliDescription("Source scene GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public SceneGameObjectReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Prefab asset path to create.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetCreatable, AssetKind = UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath Path { get; init; }
}
