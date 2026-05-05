namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Parses public camel-case literals into enum values that use PascalCase member names. </summary>
internal static class IpcCamelCaseEnumLiteralParser
{
    public static bool TryParse<TEnum> (
        string literal,
        out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrEmpty(literal))
        {
            return false;
        }

        Span<char> normalizedLiteral = stackalloc char[literal.Length];
        literal.AsSpan().CopyTo(normalizedLiteral);
        normalizedLiteral[0] = char.ToUpperInvariant(normalizedLiteral[0]);
        return Enum.TryParse(new string(normalizedLiteral), ignoreCase: false, out value);
    }
}
