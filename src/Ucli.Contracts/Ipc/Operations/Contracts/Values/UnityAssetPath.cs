using System.Text.Json.Serialization;

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
        : base(value)
    {
    }

    /// <summary> Converts a string to an asset path contract value. </summary>
    /// <param name="value"> The project-relative asset path. </param>
    /// <returns> The semantic asset path value. </returns>
    public static implicit operator UnityAssetPath (string value)
    {
        return new UnityAssetPath(value);
    }
}
