using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Provides reusable normalization helpers for optional string values. </summary>
internal static class StringValueNormalizer
{
    /// <summary> Trims one string and converts null, empty, or whitespace-only input to <see langword="null" />. </summary>
    /// <param name="value"> The input string value. </param>
    /// <returns> The trimmed value, or <see langword="null" /> when input is null, empty, or whitespace-only. </returns>
    public static string? TrimToNull (string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary> Trims one string and returns whether the result contains non-whitespace characters. </summary>
    /// <param name="value"> The input string value. </param>
    /// <param name="normalizedValue"> The trimmed value when input contains non-whitespace characters; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when input contains non-whitespace characters; otherwise <see langword="false" />. </returns>
    public static bool TryTrimToNonEmpty (
        string? value,
        [NotNullWhen(true)] out string? normalizedValue)
    {
        normalizedValue = TrimToNull(value);
        return normalizedValue is not null;
    }

    /// <summary> Trims one string and returns an empty string when input is null, empty, or whitespace-only. </summary>
    /// <param name="value"> The input string value. </param>
    /// <returns> The trimmed value, or an empty string when input is null, empty, or whitespace-only. </returns>
    public static string TrimOrEmpty (string? value)
    {
        return TrimOrFallback(value, string.Empty);
    }

    /// <summary> Trims one string and returns a fallback value when input is null, empty, or whitespace-only. </summary>
    /// <param name="value"> The input string value. </param>
    /// <param name="fallback"> The fallback value to return when input is null, empty, or whitespace-only. </param>
    /// <returns> The trimmed value, or <paramref name="fallback" /> when input is null, empty, or whitespace-only. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="fallback" /> is <see langword="null" />. </exception>
    public static string TrimOrFallback (
        string? value,
        string fallback)
    {
        if (fallback == null)
        {
            throw new ArgumentNullException(nameof(fallback));
        }

        return TrimToNull(value) ?? fallback;
    }
}