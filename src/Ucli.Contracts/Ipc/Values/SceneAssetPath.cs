using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a project-relative path to a Unity scene asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to a Unity scene asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
public sealed class SceneAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="SceneAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative scene asset path. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> does not identify a <c>.unity</c> file below <c>Assets/</c>. </exception>
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
