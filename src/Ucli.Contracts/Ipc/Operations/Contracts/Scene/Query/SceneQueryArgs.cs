using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

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
