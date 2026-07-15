using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject creation operation arguments.")]
[UcliExclusiveRequiredPropertySet("scene")]
[UcliExclusiveRequiredPropertySet("parent")]
public sealed record GoCreateArgs
{
    [JsonConstructor]
    public GoCreateArgs (
        string name,
        SceneAssetPath? scene,
        GameObjectReferenceArgs? parent)
    {
        Name = ContractArgumentGuard.RequireValue(name, nameof(name));
        Scene = scene;
        Parent = parent;
    }

    [UcliRequired]
    [UcliDescription("Name assigned to the created GameObject.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    public string Name { get; }

    [UcliDescription("Scene asset path that receives the new root GameObject.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Scene)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SceneAssetPath? Scene { get; }

    [UcliDescription("Optional parent GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameObjectReferenceArgs? Parent { get; }
}
