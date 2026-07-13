using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to an existing Unity asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to an existing Unity asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Asset)]
public sealed record UnityAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative asset path. </param>
    [JsonConstructor]
    public UnityAssetPath (string value)
        : base(UnityAssetPathContract.NormalizeAssetsDescendantPathOrThrow(value))
    {
    }
}
