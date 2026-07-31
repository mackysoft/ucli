using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("GameObject describe operation result.")]
public sealed record GameObjectDescriptionResult
{
    /// <summary> Initializes a GameObject description with owned component and child snapshots. </summary>
    /// <param name="name"> The non-null GameObject name. An empty name is valid in Unity. </param>
    /// <param name="globalObjectId"> The stable identifier, or <see langword="null" /> when unavailable. </param>
    /// <param name="components"> The non-null component descriptions, none of which may be null. </param>
    /// <param name="children"> The non-null child descriptions, none of which may be null. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="name" />, <paramref name="components" />, or <paramref name="children" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="components" /> or <paramref name="children" /> contains a <see langword="null" /> item. </exception>
    [JsonConstructor]
    public GameObjectDescriptionResult (
        string name,
        UnityGlobalObjectId? globalObjectId,
        IReadOnlyList<GameObjectComponentDescriptionResult> components,
        IReadOnlyList<GameObjectDescriptionResult> children)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        GlobalObjectId = globalObjectId;
        Components = ContractArgumentGuard.RequireItems(components, nameof(components));
        Children = ContractArgumentGuard.RequireItems(children, nameof(children));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("GameObject name.")]
    public string Name { get; private init; }

    [Description("Stable GameObject GlobalObjectId when available.")]
    public UnityGlobalObjectId? GlobalObjectId { get; }

    [JsonInclude]
    [JsonRequired]
    [Description("Components attached to this GameObject.")]
    public IReadOnlyList<GameObjectComponentDescriptionResult> Components { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Child GameObject descriptions.")]
    public IReadOnlyList<GameObjectDescriptionResult> Children { get; private init; }
}
