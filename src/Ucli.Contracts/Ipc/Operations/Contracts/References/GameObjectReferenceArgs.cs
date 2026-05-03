using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject reference accepted by GameObject operations.")]
[UcliRequiredPropertyAlternative(UcliOperationContractPropertyNames.Alias)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
public sealed record GameObjectReferenceArgs
{
    [JsonConstructor]
    public GameObjectReferenceArgs (
        UcliPlanAlias? alias,
        UnityGlobalObjectId? globalObjectId,
        PrefabAssetPath? prefab,
        SceneAssetPath? scene,
        UnityHierarchyPath? hierarchyPath)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        Prefab = prefab;
        Scene = scene;
        HierarchyPath = hierarchyPath;
    }

    public GameObjectReferenceArgs (
        string? alias,
        string? globalObjectId,
        string? prefab,
        string? scene,
        string? hierarchyPath)
        : this(
            alias == null ? null : new UcliPlanAlias(alias),
            globalObjectId == null ? null : new UnityGlobalObjectId(globalObjectId),
            prefab == null ? null : new PrefabAssetPath(prefab),
            scene == null ? null : new SceneAssetPath(scene),
            hierarchyPath == null ? null : new UnityHierarchyPath(hierarchyPath))
    {
    }

    [UcliDescription("Request-local alias produced by an earlier plan step.")]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliPlanAlias? Alias { get; init; }

    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityGlobalObjectId? GlobalObjectId { get; init; }

    [UcliDescription("Prefab asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrefabAssetPath? Prefab { get; init; }

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SceneAssetPath? Scene { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPath? HierarchyPath { get; init; }
}
