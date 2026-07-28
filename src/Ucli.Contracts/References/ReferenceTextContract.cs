using System.Text.RegularExpressions;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Owns the shared lexical rules for open artifact and execution reference values. </summary>
internal static class ReferenceTextContract
{
    internal const string DotSeparatedLowerCamelPattern =
        "^[a-z][A-Za-z0-9]*(\\.[a-z][A-Za-z0-9]*)*$(?![\\s\\S])";

    internal const string ArtifactUriPattern =
        "^[A-Za-z][A-Za-z0-9+.-]*:([A-Za-z0-9._~!$&'()*+,;=:/?@\\[\\]-]|%[0-9A-Fa-f]{2})+$(?![\\s\\S])";

    internal const string ArtifactMediaTypePattern =
        "^[a-z0-9!#$%&'*+.^_`|~-]+/[a-z0-9!#$%&'*+.^_`|~-]+(; [a-z0-9!#$%&'*+.^_`|~-]+=[A-Za-z0-9!#$%&'*+.^_`|~-]+)*$(?![\\s\\S])";

    private static readonly TimeSpan ReferencePatternMatchTimeout =
        TimeSpan.FromMilliseconds(250);

    private static readonly Regex ArtifactUriExpression = new(
        ArtifactUriPattern,
        RegexOptions.CultureInvariant,
        ReferencePatternMatchTimeout);

    private static readonly Regex ArtifactMediaTypeExpression = new(
        ArtifactMediaTypePattern,
        RegexOptions.CultureInvariant,
        ReferencePatternMatchTimeout);

    /// <summary> Validates the common lexical form used by open artifact and execution vocabulary carriers. </summary>
    /// <param name="value"> The candidate text. </param>
    /// <param name="semanticName"> The contract value name used in a validation failure. </param>
    /// <returns> The unchanged validated text. </returns>
    public static string ValidateDotSeparatedLowerCamel (
        string? value,
        string semanticName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!IsDotSeparatedLowerCamel(value))
        {
            throw new ArgumentException(
                $"{semanticName} must contain dot-separated lower-camel identifier segments.",
                nameof(value));
        }

        return value;
    }

    /// <summary> Validates an opaque locator that cannot contain whitespace. </summary>
    /// <param name="value"> The candidate locator. </param>
    /// <param name="semanticName"> The contract value name used in a validation failure. </param>
    /// <returns> The unchanged validated locator. </returns>
    public static string ValidateNonWhitespace (
        string? value,
        string semanticName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length == 0)
        {
            throw new ArgumentException(
                $"{semanticName} must not be empty.",
                nameof(value));
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                throw new ArgumentException(
                    $"{semanticName} must not contain whitespace.",
                    nameof(value));
            }
        }

        return value;
    }

    /// <summary> Validates the common lexical form of an absolute artifact URI. </summary>
    /// <param name="value"> The candidate URI text. </param>
    /// <returns> The unchanged validated URI text. </returns>
    public static string ValidateArtifactUri (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!IsMatch(ArtifactUriExpression, value))
        {
            throw new ArgumentException(
                "Artifact URI must contain an absolute scheme and a non-empty URI character sequence with valid percent encoding.",
                nameof(value));
        }

        return value;
    }

    /// <summary> Validates the canonical lexical form of an artifact media type. </summary>
    /// <param name="value"> The candidate media type text. </param>
    /// <returns> The unchanged validated media type text. </returns>
    public static string ValidateArtifactMediaType (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!IsMatch(ArtifactMediaTypeExpression, value))
        {
            throw new ArgumentException(
                "Artifact media type must use canonical lowercase type, subtype, and parameter names with token-valued parameters.",
                nameof(value));
        }

        return value;
    }

    private static bool IsDotSeparatedLowerCamel (string value)
    {
        var requiresSegmentStart = true;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (requiresSegmentStart)
            {
                if (character is not (>= 'a' and <= 'z'))
                {
                    return false;
                }

                requiresSegmentStart = false;
                continue;
            }

            if (character == '.')
            {
                requiresSegmentStart = true;
                continue;
            }

            if (!(character is >= 'a' and <= 'z')
                && !(character is >= 'A' and <= 'Z')
                && !(character is >= '0' and <= '9'))
            {
                return false;
            }
        }

        return !requiresSegmentStart;
    }

    private static bool IsMatch (Regex expression, string value)
    {
        return RegexPatternUtilities.TryIsMatch(
                value,
                expression,
                out var isMatch)
            && isMatch;
    }

}
