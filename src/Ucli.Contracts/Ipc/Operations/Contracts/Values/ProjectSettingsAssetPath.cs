using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to an existing ProjectSettings asset. </summary>
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
        : base(value)
    {
    }

    /// <summary> Converts a string to a ProjectSettings asset path contract value. </summary>
    /// <param name="value"> The project-relative ProjectSettings asset path. </param>
    /// <returns> The semantic ProjectSettings asset path value. </returns>
    public static implicit operator ProjectSettingsAssetPath (string value)
    {
        return new ProjectSettingsAssetPath(value);
    }
}
