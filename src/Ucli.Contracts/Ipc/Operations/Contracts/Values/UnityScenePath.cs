using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to a Unity scene under <c>Assets/</c> or <c>Packages/</c>. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to a Unity scene under Assets or Packages.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
public sealed class UnityScenePath : UcliStringValue
{
    /// <summary> Initializes a normalized Unity scene path. </summary>
    /// <param name="value"> The project-relative scene path. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> does not identify a <c>.unity</c> file below <c>Assets/</c> or <c>Packages/</c>. </exception>
    [JsonConstructor]
    public UnityScenePath (string value)
        : base(UnityAssetPathContract.NormalizeUnityScenePathOrThrow(value))
    {
    }

    /// <summary> Attempts to parse and normalize one Unity scene path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The normalized typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value identifies a <c>.unity</c> file below <c>Assets/</c> or <c>Packages/</c>; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityScenePath? path)
    {
        path = null;
        if (!UnityAssetPathContract.TryNormalizeUnityScenePath(value, out var normalizedPath))
        {
            return false;
        }

        path = new UnityScenePath(normalizedPath);
        return true;
    }
}
