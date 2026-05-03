using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset reference accepted by asset operations.")]
[UcliRequiredPropertyAlternative(UcliOperationContractPropertyNames.Alias)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.AssetGuid)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.AssetPath)]
[UcliRequiredPropertyAlternative(IpcResolveSelectorPropertyNames.ProjectAssetPath)]
public sealed record AssetReferenceArgs
{
    [JsonConstructor]
    public AssetReferenceArgs (
        string? alias,
        string? globalObjectId,
        string? assetGuid,
        string? assetPath,
        string? projectAssetPath)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        AssetGuid = assetGuid;
        AssetPath = assetPath;
        ProjectAssetPath = projectAssetPath;
    }

    [UcliDescription("Temporary plan alias produced earlier in the same request.")]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Alias { get; init; }

    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GlobalObjectId { get; init; }

    [UcliDescription("Asset GUID selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetGuid { get; init; }

    [UcliDescription("Asset path selector under the Unity project.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetPath { get; init; }

    [UcliDescription("Project-scoped asset path selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectAssetPath { get; init; }
}
