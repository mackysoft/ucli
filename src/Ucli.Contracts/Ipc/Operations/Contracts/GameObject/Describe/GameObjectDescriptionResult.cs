using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject describe operation result.")]
public sealed record GameObjectDescriptionResult
{
    [JsonConstructor]
    public GameObjectDescriptionResult (
        string name,
        UnityGlobalObjectId? globalObjectId,
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

    [UcliDescription("Stable GameObject GlobalObjectId when available.")]
    public UnityGlobalObjectId? GlobalObjectId { get; init; }

    [UcliRequired]
    [UcliDescription("Components attached to this GameObject.")]
    public IReadOnlyList<GameObjectComponentDescriptionResult> Components { get; init; }

    [UcliRequired]
    [UcliDescription("Child GameObject descriptions.")]
    public IReadOnlyList<GameObjectDescriptionResult> Children { get; init; }
}
