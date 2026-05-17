namespace MackySoft.Ucli.Contracts;

/// <summary> Defines the shared contract for machine-readable code values used by the code catalog. </summary>
public static class UcliCodeValue
{
    /// <summary> Gets the maximum supported code value length. </summary>
    public const int MaximumLength = 128;

    /// <summary> Determines whether the specified value is a valid uCLI code value. </summary>
    /// <param name="value"> The raw code value. </param>
    /// <returns> <see langword="true" /> when the value is an uppercase machine token accepted by <c>ucli codes</c>; otherwise <see langword="false" />. </returns>
    public static bool IsValidValue (string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        var segmentStart = true;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (segmentStart)
            {
                if (!IsUppercaseAsciiLetter(character))
                {
                    return false;
                }

                segmentStart = false;
                continue;
            }

            if (character == '.')
            {
                segmentStart = true;
                continue;
            }

            if (!IsUppercaseAsciiLetter(character) && !IsAsciiDigit(character) && character != '_')
            {
                return false;
            }
        }

        return !segmentStart;
    }

    private static bool IsUppercaseAsciiLetter (char character)
    {
        return character >= 'A' && character <= 'Z';
    }

    private static bool IsAsciiDigit (char character)
    {
        return character >= '0' && character <= '9';
    }
}
