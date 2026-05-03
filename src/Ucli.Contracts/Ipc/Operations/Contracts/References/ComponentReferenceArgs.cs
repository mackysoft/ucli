using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component reference accepted by component operations.")]
[UcliRequiredPropertyAlternative(UcliOperationContractPropertyNames.Alias)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath, IpcResolveSelectorPropertyNames.ComponentType)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath, IpcResolveSelectorPropertyNames.ComponentType)]
public sealed record ComponentReferenceArgs
{
    [JsonConstructor]
    public ComponentReferenceArgs (
        string? alias,
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

    public ComponentReferenceArgs (
        string? alias,
        string? globalObjectId,
        string? scene,
        string? prefab,
        string? hierarchyPath,
        string? componentType)
        : this(
            alias,
            globalObjectId == null ? null : new UnityGlobalObjectId(globalObjectId),
            scene == null ? null : new SceneAssetPath(scene),
            prefab == null ? null : new PrefabAssetPath(prefab),
            hierarchyPath == null ? null : new UnityHierarchyPath(hierarchyPath),
            componentType == null ? null : new UnityComponentTypeId(componentType))
    {
    }

    [UcliDescription("Temporary plan alias produced earlier in the same request.")]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Alias { get; init; }

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
