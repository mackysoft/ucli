using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines syntax rules for slash-separated Unity hierarchy paths. </summary>
internal static class UnityHierarchyPathContract
{
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

        if (string.IsNullOrWhiteSpace(value)
            || StringValueValidator.HasOuterWhitespace(value)
            || value[0] == '/'
            || value[value.Length - 1] == '/'
            || value.Contains("//", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity hierarchy path must contain non-empty slash-separated object names without outer whitespace.",
                nameof(value));
        }

        return value;
    }
}
