namespace MackySoft.Ucli.Hosting.Cli.Errors;

/// <summary> Validates public CLI error-code arguments before projecting them into catalog payloads. </summary>
internal static class ErrorCodeCliArgumentValidator
{
    private const int MaximumCodeLength = 128;

    /// <summary> Tries to create a CLI-safe error-code value. </summary>
    /// <param name="value"> The raw command-line argument. </param>
    /// <param name="code"> The validated code when successful. </param>
    /// <param name="errorMessage"> The invalid-argument message when validation fails. </param>
    /// <returns> <see langword="true" /> when the input is valid; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out UcliErrorCode code,
        out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            code = default;
            errorMessage = "Error code must not be empty.";
            return false;
        }

        if (!IsMachineToken(value))
        {
            code = default;
            errorMessage = "Error code must be an uppercase machine token up to 128 characters using letters, digits, underscores, and optional dot-separated segments.";
            return false;
        }

        code = new UcliErrorCode(value);
        errorMessage = string.Empty;
        return true;
    }

    private static bool IsMachineToken (string value)
    {
        if (value.Length > MaximumCodeLength)
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

            if (!IsUppercaseAsciiLetter(character)
                && !IsAsciiDigit(character)
                && character != '_')
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
