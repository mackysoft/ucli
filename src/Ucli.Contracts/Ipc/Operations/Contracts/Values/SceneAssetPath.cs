using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

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
        : base(UnityAssetPathContract.NormalizeSceneAssetPathOrThrow(value))
    {
    }

    /// <summary> Attempts to parse and normalize one Unity scene asset path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The normalized typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value identifies a <c>.unity</c> file below <c>Assets/</c>; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out SceneAssetPath? path)
    {
        path = null;
        if (!UnityAssetPathContract.TryNormalizeSceneAssetPath(value, out var normalizedPath))
        {
            return false;
        }

        path = new SceneAssetPath(normalizedPath);
        return true;
    }
}
