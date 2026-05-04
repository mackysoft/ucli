using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene query operation arguments.")]
public sealed record SceneQueryArgs
{
    [JsonConstructor]
    public SceneQueryArgs (
        SceneAssetPath scene,
        UnityHierarchyPathPrefix? pathPrefix,
        UnityComponentTypeId? componentType)
    {
        Scene = scene;
        PathPrefix = pathPrefix;
        ComponentType = componentType;
    }

    public SceneQueryArgs (
        string scene,
        string? pathPrefix,
        string? componentType)
        : this(
            new SceneAssetPath(scene),
            pathPrefix == null ? null : new UnityHierarchyPathPrefix(pathPrefix),
            componentType == null ? null : new UnityComponentTypeId(componentType))
    {
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to query.")]
    public SceneAssetPath Scene { get; init; }

    [UcliDescription("Optional hierarchy path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPathPrefix? PathPrefix { get; init; }

    [UcliDescription("Optional component type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityComponentTypeId? ComponentType { get; init; }
}
