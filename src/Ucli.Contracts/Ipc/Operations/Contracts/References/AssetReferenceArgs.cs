using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset reference accepted by asset operations.")]
[UcliExclusiveRequiredPropertySet(UcliOperationContractPropertyNames.Alias)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.AssetGuid)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.AssetPath)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.ProjectAssetPath)]
public sealed record AssetReferenceArgs
{
    [JsonConstructor]
    public AssetReferenceArgs (
        UcliPlanAlias? alias,
        UnityGlobalObjectId? globalObjectId,
        UnityAssetGuid? assetGuid,
        UnityAssetPath? assetPath,
        ProjectSettingsAssetPath? projectAssetPath)
    {
        Alias = alias;
        GlobalObjectId = globalObjectId;
        AssetGuid = assetGuid;
        AssetPath = assetPath;
        ProjectAssetPath = projectAssetPath;
    }

    public AssetReferenceArgs (
        string? alias,
        string? globalObjectId,
        string? assetGuid,
        string? assetPath,
        string? projectAssetPath)
        : this(
            alias == null ? null : new UcliPlanAlias(alias),
            globalObjectId == null ? null : new UnityGlobalObjectId(globalObjectId),
            assetGuid == null ? null : new UnityAssetGuid(assetGuid),
            assetPath == null ? null : new UnityAssetPath(assetPath),
            projectAssetPath == null ? null : new ProjectSettingsAssetPath(projectAssetPath))
    {
    }

    [UcliDescription("Request-local alias produced by an earlier plan step.")]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliPlanAlias? Alias { get; init; }

    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityGlobalObjectId? GlobalObjectId { get; init; }

    [UcliDescription("Asset GUID selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityAssetGuid? AssetGuid { get; init; }

    [UcliDescription("Asset path selector under the Unity project.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityAssetPath? AssetPath { get; init; }

    [UcliDescription("Project-scoped asset path selector.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectSettingsAssetPath? ProjectAssetPath { get; init; }
}
