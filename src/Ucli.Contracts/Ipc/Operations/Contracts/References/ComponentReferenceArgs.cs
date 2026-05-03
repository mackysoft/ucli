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
        string? globalObjectId,
        string? scene,
        string? prefab,
        string? hierarchyPath,
        string? componentType)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        Scene = scene;
        Prefab = prefab;
        HierarchyPath = hierarchyPath;
        ComponentType = componentType;
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

    [UcliDescription("Prefab asset path for a hierarchy selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prefab { get; init; }

    [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HierarchyPath { get; init; }

    [UcliDescription("Component type identifier.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ComponentType { get; init; }
}
