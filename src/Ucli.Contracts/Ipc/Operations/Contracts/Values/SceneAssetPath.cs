using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to an existing Unity scene asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to an existing Unity scene asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Scene)]
public sealed record SceneAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="SceneAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative scene asset path. </param>
    [JsonConstructor]
    public SceneAssetPath (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a scene asset path contract value. </summary>
    /// <param name="value"> The project-relative scene asset path. </param>
    /// <returns> The semantic scene asset path value. </returns>
    public static implicit operator SceneAssetPath (string value)
    {
        return new SceneAssetPath(value);
    }
}
