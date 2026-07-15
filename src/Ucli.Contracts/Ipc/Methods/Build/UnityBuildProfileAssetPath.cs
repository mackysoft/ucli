using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a canonical Unity Build Profile asset path below <c>Assets/</c>. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class UnityBuildProfileAssetPath : UcliStringValue
{
    /// <summary> Initializes a canonical Unity Build Profile asset path. </summary>
    /// <param name="value"> The slash-separated project-relative path below <c>Assets/</c>. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is not a normalized <c>Assets/</c> descendant or identifies a <c>.meta</c> file.
    /// </exception>
    [JsonConstructor]
    public UnityBuildProfileAssetPath (string value)
        : base(Validate(value))
    {
    }

    /// <summary> Attempts to parse a canonical Unity Build Profile asset path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="value" /> is a normalized <c>Assets/</c> descendant that does not identify a <c>.meta</c> file;
    /// otherwise <see langword="false" />.
    /// </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityBuildProfileAssetPath? path)
    {
        path = null;
        if (!TryValidate(value))
        {
            return false;
        }

        path = new UnityBuildProfileAssetPath(value!);
        return true;
    }

    private static string Validate (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!TryValidate(value))
        {
            throw new ArgumentException(
                "Unity Build Profile asset path must be a normalized Assets descendant and must not identify a .meta file.",
                nameof(value));
        }

        return value;
    }

    private static bool TryValidate (string? value)
    {
        return value != null
            && UnityAssetPathContract.IsNormalizedAssetsDescendantPath(value)
            && !value.EndsWith(UnityAssetPathContract.MetaFileExtension, StringComparison.OrdinalIgnoreCase);
    }
}
