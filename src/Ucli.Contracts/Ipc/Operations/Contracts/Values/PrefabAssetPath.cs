using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to an existing Unity prefab asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to an existing Unity prefab asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Prefab)]
public sealed record PrefabAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="PrefabAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative prefab asset path. </param>
    [JsonConstructor]
    public PrefabAssetPath (string value)
        : base(UnityAssetPathContract.NormalizePrefabAssetPathOrThrow(value))
    {
    }

    /// <summary> Attempts to parse and normalize one Unity prefab asset path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The normalized typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value identifies a <c>.prefab</c> file below <c>Assets/</c>; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out PrefabAssetPath? path)
    {
        path = null;
        if (!UnityAssetPathContract.TryNormalizePrefabAssetPath(value, out var normalizedPath))
        {
            return false;
        }

        path = new PrefabAssetPath(normalizedPath);
        return true;
    }
}
