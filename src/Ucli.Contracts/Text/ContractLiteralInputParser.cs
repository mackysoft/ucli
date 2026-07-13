namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Parses enum-backed contract literals at input boundaries that accept user spelling variations. </summary>
internal static class ContractLiteralInputParser
{
    public static bool IsDefinedIgnoreCase<TEnum> (string? literal)
        where TEnum : struct, Enum
    {
        return literal != null && IsDefinedWithComparison<TEnum>(literal, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseIgnoreCase<TEnum> (
        string? literal,
        out TEnum value)
        where TEnum : struct, Enum
    {
        if (literal is null)
        {
            value = default;
            return false;
        }

        return TryParseWithComparison(literal, StringComparison.OrdinalIgnoreCase, out value);
    }

    public static bool TryParseTrimmed<TEnum> (
        string? literal,
        out TEnum value)
        where TEnum : struct, Enum
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(literal, out var normalizedLiteral))
        {
            value = default;
            return false;
        }

        return ContractLiteralCodec.TryParse(normalizedLiteral, out value);
    }

    private static bool TryParseWithComparison<TEnum> (
        string literal,
        StringComparison comparison,
        out TEnum value)
        where TEnum : struct, Enum
    {
        foreach (var candidateLiteral in ContractLiteralCodec.GetLiterals<TEnum>())
        {
            if (string.Equals(literal, candidateLiteral, comparison))
            {
                return ContractLiteralCodec.TryParse(candidateLiteral, out value);
            }
        }

        value = default;
        return false;
    }

    private static bool IsDefinedWithComparison<TEnum> (
        string literal,
        StringComparison comparison)
        where TEnum : struct, Enum
    {
        foreach (var candidateLiteral in ContractLiteralCodec.GetLiterals<TEnum>())
        {
            if (string.Equals(literal, candidateLiteral, comparison))
            {
                return true;
            }
        }

        return false;
    }
}
