using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Object selector accepted by ucli.resolve.")]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.AssetGuid)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.AssetPath)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.ProjectAssetPath)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath)]
[UcliPropertyDependency(IpcResolveSelectorPropertyNames.ComponentType, IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
public sealed record ResolveSelectorArgs
{
    [JsonConstructor]
    public ResolveSelectorArgs (
        string? globalObjectId,
        string? assetGuid,
        string? assetPath,
        string? projectAssetPath,
        string? scene,
        string? prefab,
        string? hierarchyPath,
        string? componentType)
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
    public string? GlobalObjectId { get; init; }

    [UcliDescription("Asset GUID selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetGuid { get; init; }

    [UcliDescription("Asset path selector under the Unity project.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetPath { get; init; }

    [UcliDescription("Project-scoped asset path selector, such as ProjectSettings assets.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectAssetPath { get; init; }

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scene { get; init; }

    [UcliDescription("Prefab asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prefab { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HierarchyPath { get; init; }

    [UcliDescription("Component type identifier for component selection.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ComponentType { get; init; }
}
