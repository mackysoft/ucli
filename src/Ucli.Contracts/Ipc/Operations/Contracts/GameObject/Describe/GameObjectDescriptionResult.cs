using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject describe operation result.")]
public sealed record GameObjectDescriptionResult
{
    [JsonConstructor]
    public GameObjectDescriptionResult (
        string name,
        string globalObjectId,
        IReadOnlyList<GameObjectComponentDescriptionResult> components,
        IReadOnlyList<GameObjectDescriptionResult> children)
    {
        Name = name;
        GlobalObjectId = globalObjectId;
        Components = components;
        Children = children;
    }

    [UcliRequired]
    [UcliDescription("GameObject name.")]
    public string Name { get; init; }

    [UcliRequired]
    [UcliDescription("GameObject GlobalObjectId.")]
    public string GlobalObjectId { get; init; }

    [UcliRequired]
    [UcliDescription("Components attached to this GameObject.")]
    public IReadOnlyList<GameObjectComponentDescriptionResult> Components { get; init; }

    [UcliRequired]
    [UcliDescription("Child GameObject descriptions.")]
    public IReadOnlyList<GameObjectDescriptionResult> Children { get; init; }
}
