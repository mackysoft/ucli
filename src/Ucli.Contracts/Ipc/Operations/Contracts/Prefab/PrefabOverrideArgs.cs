using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Prefab instance property override operation arguments.")]
public sealed record PrefabOverrideArgs
{
    [JsonConstructor]
    public PrefabOverrideArgs (
        ComponentReferenceArgs target,
        PrefabAssetPath targetAssetPath,
        IReadOnlyList<SerializedPropertyPath>? propertyPaths)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        TargetAssetPath = ContractArgumentGuard.RequireNotNull(targetAssetPath, nameof(targetAssetPath));
        PropertyPaths = propertyPaths is null
            ? null
            : ContractArgumentGuard.RequireItems(propertyPaths, nameof(propertyPaths));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Prefab instance component target.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.Component)]
    public ComponentReferenceArgs Target { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Prefab asset path that receives or provides the property override.")]
    [UcliAssetExists(UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath TargetAssetPath { get; private init; }

    [Description("Exact SerializedProperty paths changed by a preceding set action. Omit to use all request-attributed paths.")]
    [UcliSerializedProperty(UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedPropertyPath>? PropertyPaths { get; }
}
