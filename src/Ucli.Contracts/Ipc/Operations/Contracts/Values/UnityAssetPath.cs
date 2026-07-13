using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to a Unity asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to a Unity asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
public sealed record UnityAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative asset path. </param>
    [JsonConstructor]
    public UnityAssetPath (string value)
        : base(UnityAssetPathContract.NormalizeAssetsDescendantPathOrThrow(value))
    {
    }

    /// <summary> Attempts to parse and normalize one Unity asset path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The normalized typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value identifies an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityAssetPath? path)
    {
        path = null;
        if (!UnityAssetPathContract.TryNormalizeAssetsDescendantPath(value, out var normalizedPath))
        {
            return false;
        }

        path = new UnityAssetPath(normalizedPath);
        return true;
    }
}
