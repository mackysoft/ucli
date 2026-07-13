using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to an existing ProjectSettings asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to an existing ProjectSettings asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.ProjectSettings)]
public sealed record ProjectSettingsAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="ProjectSettingsAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative ProjectSettings asset path. </param>
    [JsonConstructor]
    public ProjectSettingsAssetPath (string value)
        : base(UnityAssetPathContract.NormalizeProjectSettingsDescendantPathOrThrow(value))
    {
    }
}
