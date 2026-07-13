using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to an existing Unity prefab asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
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
}
