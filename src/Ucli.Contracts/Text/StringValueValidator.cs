namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Provides reusable validation helpers for string values. </summary>
internal static class StringValueValidator
{
    /// <summary> Determines whether a value contains leading or trailing whitespace. </summary>
    /// <param name="value"> The string value to inspect. </param>
    /// <returns> <see langword="true" /> when leading or trailing whitespace exists; otherwise <see langword="false" />. </returns>
    public static bool HasOuterWhitespace (string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]);
    }

    /// <summary> Determines whether a value contains well-formed UTF-16 text. </summary>
    /// <param name="value"> The string value to inspect. </param>
    /// <returns> <see langword="true" /> when every surrogate is paired; otherwise <see langword="false" />. </returns>
    public static bool IsWellFormedUtf16 (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return !HasInvalidCharacter(value, rejectControlCharacters: false);
    }

    /// <summary> Determines whether a value contains a control character or malformed UTF-16 text. </summary>
    /// <param name="value"> The string value to inspect. </param>
    /// <returns> <see langword="true" /> when either condition is present; otherwise <see langword="false" />. </returns>
    public static bool HasControlCharacterOrMalformedUtf16 (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return HasInvalidCharacter(value, rejectControlCharacters: true);
    }

    private static bool HasInvalidCharacter (
        string value,
        bool rejectControlCharacters)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (rejectControlCharacters && char.IsControl(character))
            {
                return true;
            }

            if (!char.IsSurrogate(character))
            {
                continue;
            }

            if (char.IsHighSurrogate(character)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                index++;
                continue;
            }

            return true;
        }

        return false;
    }
}
