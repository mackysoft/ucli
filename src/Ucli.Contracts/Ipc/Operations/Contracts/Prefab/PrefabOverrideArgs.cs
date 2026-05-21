using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Prefab instance property override operation arguments.")]
public sealed record PrefabOverrideArgs
{
    [JsonConstructor]
    public PrefabOverrideArgs (
        ComponentReferenceArgs target,
        PrefabAssetPath targetAssetPath,
        IReadOnlyList<SerializedPropertyPath>? propertyPaths = null)
    {
        Target = target;
        TargetAssetPath = targetAssetPath;
        PropertyPaths = propertyPaths;
    }

    [UcliRequired]
    [UcliDescription("Prefab instance component target.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Component)]
    public ComponentReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Prefab asset path that receives or provides the property override.")]
    public PrefabAssetPath TargetAssetPath { get; init; }

    [UcliDescription("Exact SerializedProperty paths changed by a preceding set action. Omit to use all request-attributed paths.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.SerializedProperty, Access = UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedPropertyPath>? PropertyPaths { get; init; }
}
