using System.Diagnostics.CodeAnalysis;
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

    /// <summary> Attempts to parse and normalize one ProjectSettings asset path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The normalized typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value identifies a <c>ProjectSettings/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out ProjectSettingsAssetPath? path)
    {
        path = null;
        if (!UnityAssetPathContract.TryNormalizeProjectSettingsDescendantPath(value, out var normalizedPath))
        {
            return false;
        }

        path = new ProjectSettingsAssetPath(normalizedPath);
        return true;
    }
}
