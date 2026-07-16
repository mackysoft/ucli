using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset reference accepted by asset operations.")]
[UcliExclusiveRequiredPropertySet(UcliOperationContractPropertyNames.Alias)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.GlobalObjectId)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.AssetGuid)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.AssetPath)]
[UcliExclusiveRequiredPropertySet(IpcResolveSelectorPropertyNames.ProjectAssetPath)]
public sealed record AssetReferenceArgs
{
    /// <exception cref="ArgumentException"> Thrown when <paramref name="assetGuid" /> is <see cref="Guid.Empty" />. </exception>
    [JsonConstructor]
    public AssetReferenceArgs (
        UcliPlanAlias? alias,
        UnityGlobalObjectId? globalObjectId,
        Guid? assetGuid,
        UnityAssetPath? assetPath,
        ProjectSettingsAssetPath? projectAssetPath)
    {
        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Asset GUID must not be empty.", nameof(assetGuid));
        }

        Alias = alias;
        GlobalObjectId = globalObjectId;
        AssetGuid = assetGuid;
        AssetPath = assetPath;
        ProjectAssetPath = projectAssetPath;
    }

    [UcliDescription("Request-local alias produced by an earlier plan step.")]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliPlanAlias? Alias { get; }

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

    [UcliDescription("Project-scoped asset path selector.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.ProjectSettings)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectSettingsAssetPath? ProjectAssetPath { get; }
}
