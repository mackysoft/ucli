using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Object selector accepted by ucli.resolve.")]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.AssetGuid)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.AssetPath)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.ProjectAssetPath)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath)]
[UcliPropertyRequires(IpcResolveSelectorPropertyNames.ComponentType, IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
public sealed record ResolveSelectorArgs
{
    [JsonConstructor]
    public ResolveSelectorArgs (
        UnityGlobalObjectId? globalObjectId,
        UnityAssetGuid? assetGuid,
        UnityAssetPath? assetPath,
        ProjectSettingsAssetPath? projectAssetPath,
        SceneAssetPath? scene,
        PrefabAssetPath? prefab,
        UnityHierarchyPath? hierarchyPath,
        UnityComponentTypeId? componentType)
    {
        GlobalObjectId = globalObjectId;
        AssetGuid = assetGuid;
        AssetPath = assetPath;
        ProjectAssetPath = projectAssetPath;
        Scene = scene;
        Prefab = prefab;
        HierarchyPath = hierarchyPath;
        ComponentType = componentType;
    }

    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityGlobalObjectId? GlobalObjectId { get; init; }

    [UcliDescription("Asset GUID selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityAssetGuid? AssetGuid { get; init; }

    [UcliDescription("Asset path selector under the Unity project.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityAssetPath? AssetPath { get; init; }

    [UcliDescription("Project-scoped asset path selector, such as ProjectSettings assets.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectSettingsAssetPath? ProjectAssetPath { get; init; }

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SceneAssetPath? Scene { get; init; }

    [UcliDescription("Prefab asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrefabAssetPath? Prefab { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPath? HierarchyPath { get; init; }

    [UcliDescription("Component type identifier for component selection.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityComponentTypeId? ComponentType { get; init; }
}
