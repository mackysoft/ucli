using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Prefab instance property override operation arguments.")]
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

    [UcliRequired]
    [UcliDescription("Prefab instance component target.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Component)]
    public ComponentReferenceArgs Target { get; }

    [UcliRequired]
    [UcliDescription("Prefab asset path that receives or provides the property override.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath TargetAssetPath { get; }

    [UcliDescription("Exact SerializedProperty paths changed by a preceding set action. Omit to use all request-attributed paths.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.SerializedProperty, Access = UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedPropertyPath>? PropertyPaths { get; }
}
