using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts;

/// <summary> Carries a canonical portable artifact path relative to the repository root. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
[Pattern(
    """^([^./:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000][^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000]*|\.[^./:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000][^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000]*|\.\.[^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000][^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000]*)(/([^./:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000][^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000]*|\.[^./:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000][^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000]*|\.\.[^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000][^/:\\\u0000-\u0020\u007F-\u009F\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000]*))*$(?![\s\S])""")]
public sealed class ArtifactPath : UcliStringValue
{
    /// <summary> Initializes a canonical slash-separated repository-relative artifact path. </summary>
    /// <param name="value"> The portable artifact path relative to the repository root. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is not canonical portable relative path text, contains whitespace, or contains an empty, current-directory, or parent-directory segment.
    /// </exception>
    [JsonConstructor]
    public ArtifactPath (string value)
        : base(Validate(value))
    {
    }

    /// <summary> Attempts to parse canonical portable artifact-path text. </summary>
    /// <param name="value"> The candidate path. </param>
    /// <param name="path"> The parsed path when successful; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is canonical portable relative path text; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out ArtifactPath? path)
    {
        path = null;
        if (!IsValid(value))
        {
            return false;
        }

        path = new ArtifactPath(value!);
        return true;
    }

    private static string Validate (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!IsValid(value))
        {
            throw new ArgumentException(
                "Artifact path must be whitespace-free canonical portable path text relative to the repository root.",
                nameof(value));
        }

        return value;
    }

    private static bool IsValid (string? value)
    {
        if (!RelativePathContract.IsNormalized(value))
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
