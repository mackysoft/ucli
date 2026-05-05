namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Provides reusable conversion helpers for enum/string literal codecs. </summary>
internal static class LiteralCodecUtilities
{
    /// <summary> Converts one enum value to mapped literal string. </summary>
    /// <typeparam name="TValue"> The enum-like value type. </typeparam>
    /// <param name="value"> The value to convert. </param>
    /// <param name="mappings"> The value-literal mapping table. </param>
    /// <param name="paramName"> The original parameter name for out-of-range errors. </param>
    /// <param name="unsupportedMessage"> The error message for unsupported values. </param>
    /// <returns> The mapped literal string. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="value" /> is not mapped. </exception>
    public static string ToValue<TValue> (
        TValue value,
        ReadOnlySpan<(TValue Value, string Literal)> mappings,
        string paramName,
        string unsupportedMessage)
        where TValue : struct
    {
        foreach (var mapping in mappings)
        {
            if (EqualityComparer<TValue>.Default.Equals(mapping.Value, value))
            {
                return mapping.Literal;
            }
        }

        throw new ArgumentOutOfRangeException(paramName, value, unsupportedMessage);
    }

    /// <summary> Tries to parse one raw literal string to mapped enum value. </summary>
    /// <typeparam name="TValue"> The enum-like value type. </typeparam>
    /// <param name="value"> The raw literal string. </param>
    /// <param name="mappings"> The value-literal mapping table. </param>
    /// <param name="comparison"> The string comparison strategy. </param>
    /// <param name="parsedValue"> The parsed enum value on success; otherwise default value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse<TValue> (
        string? value,
        ReadOnlySpan<(TValue Value, string Literal)> mappings,
        StringComparison comparison,
        out TValue parsedValue)
        where TValue : struct
    {
        foreach (var mapping in mappings)
        {
            if (string.Equals(value, mapping.Literal, comparison))
            {
                parsedValue = mapping.Value;
                return true;
            }
        }

        parsedValue = default;
        return false;
    }

    /// <summary> Tries to trim one input literal and parse to mapped enum value. </summary>
    /// <typeparam name="TValue"> The enum-like value type. </typeparam>
    /// <param name="value"> The raw literal string. </param>
    /// <param name="mappings"> The value-literal mapping table. </param>
    /// <param name="comparison"> The string comparison strategy. </param>
    /// <param name="parsedValue"> The parsed enum value on success; otherwise default value. </param>
    /// <returns> <see langword="true" /> when trimming and parsing succeed; otherwise <see langword="false" />. </returns>
    public static bool TryParseTrimmed<TValue> (
        string? value,
        ReadOnlySpan<(TValue Value, string Literal)> mappings,
        StringComparison comparison,
        out TValue parsedValue)
        where TValue : struct
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalized))
        {
            parsedValue = default;
            return false;
        }

        return TryParse(normalized, mappings, comparison, out parsedValue);
    }

    /// <summary> Tries to trim one input literal and normalize it to canonical mapped literal. </summary>
    /// <param name="value"> The raw literal string. </param>
    /// <param name="literals"> The canonical literal set. </param>
    /// <param name="comparison"> The string comparison strategy. </param>
    /// <param name="normalizedLiteral"> The canonical literal on success; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one canonical literal is matched; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeLiteral (
        string? value,
        ReadOnlySpan<string> literals,
        StringComparison comparison,
        out string? normalizedLiteral)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalized))
        {
            normalizedLiteral = null;
            return false;
        }

        foreach (var literal in literals)
        {
            if (string.Equals(normalized, literal, comparison))
            {
                normalizedLiteral = literal;
                return true;
            }
        }

        normalizedLiteral = null;
        return false;
    }
}
