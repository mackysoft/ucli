using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to an existing Unity prefab asset. </summary>
[UcliDescription("Project-relative path to an existing Unity prefab asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Prefab)]
public sealed record PrefabAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="PrefabAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative prefab asset path. </param>
    [JsonConstructor]
    public PrefabAssetPath (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a prefab asset path contract value. </summary>
    /// <param name="value"> The project-relative prefab asset path. </param>
    /// <returns> The semantic prefab asset path value. </returns>
    public static implicit operator PrefabAssetPath (string value)
    {
        return new PrefabAssetPath(value);
    }
}
