using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Scene query operation arguments.")]
public sealed record SceneQueryArgs
{
    [JsonConstructor]
    public SceneQueryArgs (
        SceneAssetPath scene,
        UnityHierarchyPath? pathPrefix,
        UnityComponentTypeId? componentType)
    {
        Scene = ContractArgumentGuard.RequireNotNull(scene, nameof(scene));
        PathPrefix = pathPrefix;
        ComponentType = componentType;
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Scene asset path to query.")]
    [UcliAssetExists(UcliOperationAssetKind.Scene)]
    public SceneAssetPath Scene { get; private init; }

    [Description("Optional hierarchy path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPath? PathPrefix { get; }

    [Description("Optional component type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityComponentTypeId? ComponentType { get; }
}
