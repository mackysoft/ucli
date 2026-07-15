using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene query operation arguments.")]
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

    [UcliRequired]
    [UcliDescription("Scene asset path to query.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Scene)]
    public SceneAssetPath Scene { get; }

    [UcliDescription("Optional hierarchy path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPath? PathPrefix { get; }

    [UcliDescription("Optional component type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityComponentTypeId? ComponentType { get; }
}
