using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component reference accepted by component operations.")]
[UcliExclusiveRequiredPropertySet(UcliOperationContractPropertyNames.Alias)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath, IpcResolveSelectorPropertyNames.ComponentType)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath, IpcResolveSelectorPropertyNames.ComponentType)]
public sealed record ComponentReferenceArgs
{
    [JsonConstructor]
    public ComponentReferenceArgs (
        UcliPlanAlias? alias,
        UnityGlobalObjectId? globalObjectId,
        SceneAssetPath? scene,
        PrefabAssetPath? prefab,
        UnityHierarchyPath? hierarchyPath,
        UnityComponentTypeId? componentType)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        Scene = scene;
        Prefab = prefab;
        HierarchyPath = hierarchyPath;
        ComponentType = componentType;
    }

    [UcliDescription("Request-local alias produced by an earlier plan step.")]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliPlanAlias? Alias { get; init; }

    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityGlobalObjectId? GlobalObjectId { get; init; }

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SceneAssetPath? Scene { get; init; }

    [UcliDescription("Prefab asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrefabAssetPath? Prefab { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPath? HierarchyPath { get; init; }

    [UcliDescription("Component type identifier.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityComponentTypeId? ComponentType { get; init; }
}
