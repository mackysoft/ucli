using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene GameObject reference accepted by prefab creation.")]
[UcliOneOfRequired(UcliOperationContractPropertyNames.Alias)]
[UcliOneOfRequired(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliOneOfRequired(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
public sealed record SceneGameObjectReferenceArgs
{
    [JsonConstructor]
    public SceneGameObjectReferenceArgs (
        string? alias,
        string? globalObjectId,
        string? scene,
        string? hierarchyPath)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        Scene = scene;
        HierarchyPath = hierarchyPath;
    }

    [UcliDescription("Temporary plan alias produced earlier in the same request.")]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Alias { get; init; }

    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GlobalObjectId { get; init; }

    [UcliDescription("Scene asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scene { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HierarchyPath { get; init; }
}
