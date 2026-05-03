using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject reference accepted by GameObject operations.")]
[UcliOneOfRequired(UcliOperationContractPropertyNames.Alias)]
[UcliOneOfRequired(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliOneOfRequired(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath)]
[UcliOneOfRequired(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
public sealed record GameObjectReferenceArgs
{
    [JsonConstructor]
    public GameObjectReferenceArgs (
        string? alias,
        string? globalObjectId,
        string? prefab,
        string? scene,
        string? hierarchyPath)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        Prefab = prefab;
        Scene = scene;
        HierarchyPath = hierarchyPath;
    }

    [UcliDescription("Temporary plan alias produced earlier in the same request.")]
    [UcliMinLength(1)]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Alias { get; init; }

    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [UcliMinLength(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GlobalObjectId { get; init; }

    [UcliDescription("Prefab asset path for a hierarchy selector.")]
    [UcliMinLength(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prefab { get; init; }

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [UcliMinLength(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scene { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
    [UcliMinLength(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HierarchyPath { get; init; }
}
