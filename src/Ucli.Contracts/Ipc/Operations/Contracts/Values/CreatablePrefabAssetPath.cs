using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative Unity prefab path that may be created by an operation. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative Unity prefab path that may be created by an operation.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetCreatable, AssetKind = UcliOperationAssetKind.Prefab)]
public sealed record CreatablePrefabAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="CreatablePrefabAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative prefab path to create. </param>
    [JsonConstructor]
    public CreatablePrefabAssetPath (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a creatable prefab path contract value. </summary>
    /// <param name="value"> The project-relative prefab path to create. </param>
    /// <returns> The semantic creatable prefab path value. </returns>
    public static implicit operator CreatablePrefabAssetPath (string value)
    {
        return new CreatablePrefabAssetPath(value);
    }
}
