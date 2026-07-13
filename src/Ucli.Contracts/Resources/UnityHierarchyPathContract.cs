using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines syntax rules for slash-separated Unity hierarchy paths. </summary>
internal static class UnityHierarchyPathContract
{
    /// <summary> Determines whether text contains only non-empty slash-separated object names. </summary>
    /// <param name="value"> The hierarchy path text. </param>
    /// <param name="validatedValue"> The unchanged value when validation succeeds. </param>
    /// <returns> <see langword="true" /> when the hierarchy path is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidate (
        string? value,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? validatedValue)
    {
        validatedValue = null;
        if (string.IsNullOrWhiteSpace(value)
            || StringValueValidator.HasOuterWhitespace(value)
            || value[0] == '/'
            || value[value.Length - 1] == '/'
            || value.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        validatedValue = value;
        return true;
    }

    /// <summary> Validates hierarchy path text without changing Unity object names. </summary>
    /// <param name="value"> The hierarchy path text. </param>
    /// <returns> The unchanged hierarchy path when every segment is non-empty. </returns>
    /// <exception cref="ArgumentException"> Thrown when the value is empty, has outer whitespace, or contains an empty segment. </exception>
    public static string Validate (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!TryValidate(value, out var validatedValue))
        {
            throw new ArgumentException(
                "Unity hierarchy path must contain non-empty slash-separated object names without outer whitespace.",
                nameof(value));
        }

        return validatedValue;
    }
}
