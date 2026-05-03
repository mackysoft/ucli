using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject creation operation arguments.")]
[UcliOneOfRequired("scene")]
[UcliOneOfRequired("parent")]
public sealed record GoCreateArgs
{
    [JsonConstructor]
    public GoCreateArgs (
        string name,
        string? scene,
        GameObjectReferenceArgs? parent)
    {
        Name = name;
        Scene = scene;
        Parent = parent;
    }

    [UcliRequired]
    [UcliDescription("Name assigned to the created GameObject.")]
    [UcliMinLength(1)]
    public string Name { get; init; }

    [UcliDescription("Scene asset path that receives the new root GameObject.")]
    [UcliMinLength(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scene { get; init; }

    [UcliDescription("Optional parent GameObject reference.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameObjectReferenceArgs? Parent { get; init; }
}
