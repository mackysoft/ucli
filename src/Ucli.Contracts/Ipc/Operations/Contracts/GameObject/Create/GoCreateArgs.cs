using System.Text.Json.Serialization;

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
        Name = name;
        Scene = scene;
        Parent = parent;
    }

    public GoCreateArgs (
        string name,
        string? scene,
        GameObjectReferenceArgs? parent)
        : this(name, scene == null ? null : new SceneAssetPath(scene), parent)
    {
    }

    [UcliRequired]
    [UcliDescription("Name assigned to the created GameObject.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    public string Name { get; init; }

    [UcliDescription("Scene asset path that receives the new root GameObject.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SceneAssetPath? Scene { get; init; }

    [UcliDescription("Optional parent GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameObjectReferenceArgs? Parent { get; init; }
}
