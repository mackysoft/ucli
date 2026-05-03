using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene GameObject reference accepted by prefab creation.")]
[UcliRequiredPropertyAlternative(UcliOperationContractPropertyNames.Alias)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
public sealed record SceneGameObjectReferenceArgs
{
    [JsonConstructor]
    public SceneGameObjectReferenceArgs (
        UcliPlanAlias? alias,
        UnityGlobalObjectId? globalObjectId,
        SceneAssetPath? scene,
        UnityHierarchyPath? hierarchyPath)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        Scene = scene;
        HierarchyPath = hierarchyPath;
    }

    public SceneGameObjectReferenceArgs (
        string? alias,
        string? globalObjectId,
        string? scene,
        string? hierarchyPath)
        : this(
            alias == null ? null : new UcliPlanAlias(alias),
            globalObjectId == null ? null : new UnityGlobalObjectId(globalObjectId),
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

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SceneAssetPath? Scene { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityHierarchyPath? HierarchyPath { get; init; }
}
