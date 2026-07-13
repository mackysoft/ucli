using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative Unity asset path that may be created by an operation. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative Unity asset path that may be created by an operation.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetCreatable, AssetKind = UcliOperationAssetKind.Asset)]
public sealed record CreatableUnityAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="CreatableUnityAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative asset path to create. </param>
    [JsonConstructor]
    public CreatableUnityAssetPath (string value)
        : base(UnityAssetPathContract.NormalizeAssetsDescendantPathOrThrow(value))
    {
    }
}
