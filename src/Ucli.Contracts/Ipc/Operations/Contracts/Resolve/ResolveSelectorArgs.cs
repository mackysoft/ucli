using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

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
    /// <exception cref="ArgumentException"> Thrown when <paramref name="assetGuid" /> is <see cref="Guid.Empty" />. </exception>
    [JsonConstructor]
    public ResolveSelectorArgs (
        UnityGlobalObjectId? globalObjectId,
        Guid? assetGuid,
        UnityAssetPath? assetPath,
        ProjectSettingsAssetPath? projectAssetPath,
        SceneAssetPath? scene,
        PrefabAssetPath? prefab,
        UnityHierarchyPath? hierarchyPath,
        UnityComponentTypeId? componentType)
    {
        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Asset GUID must not be empty.", nameof(assetGuid));
        }

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
    public UnityGlobalObjectId? GlobalObjectId { get; }

    [UcliDescription("Asset GUID selector.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetGuid)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? AssetGuid { get; }

    [UcliDescription("Asset path selector under the Unity project.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Asset)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityAssetPath? AssetPath { get; }

    [UcliDescription("Project-scoped asset path selector, such as ProjectSettings assets.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.ProjectSettings)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectSettingsAssetPath? ProjectAssetPath { get; }

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Scene)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SceneAssetPath? Scene { get; }

    [UcliDescription("Prefab asset path for a hierarchy selector.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Prefab)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrefabAssetPath? Prefab { get; }

    [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPath? HierarchyPath { get; }

    [UcliDescription("Component type identifier for component selection.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityComponentTypeId? ComponentType { get; }
}
