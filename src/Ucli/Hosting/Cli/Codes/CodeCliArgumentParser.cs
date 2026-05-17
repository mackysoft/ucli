using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Parses public CLI code references before projecting them into catalog payloads. </summary>
internal static class CodeCliArgumentParser
{
    /// <summary> Tries to parse a CLI-safe code reference from <c>CODE</c> or <c>KIND:CODE</c>. </summary>
    /// <param name="value"> The raw command-line argument. </param>
    /// <param name="reference"> The parsed code reference when successful. </param>
    /// <param name="errorMessage"> The invalid-argument message when parsing fails. </param>
    /// <returns> <see langword="true" /> when the input is valid; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out CodeCatalogCodeReference reference,
        out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reference = null!;
            errorMessage = "Code must not be empty.";
            return false;
        }

        var expectedKind = (string?)null;
        var code = value;
        var kindSeparatorIndex = value.IndexOf(':', StringComparison.Ordinal);
        if (kindSeparatorIndex >= 0)
        {
            if (value.IndexOf(':', kindSeparatorIndex + 1) >= 0)
            {
                reference = null!;
                errorMessage = "Code reference must contain at most one kind separator.";
                return false;
            }

            expectedKind = value[..kindSeparatorIndex];
            code = value[(kindSeparatorIndex + 1)..];
            if (string.IsNullOrWhiteSpace(expectedKind))
            {
                reference = null!;
                errorMessage = "Code kind must not be empty.";
                return false;
            }

            if (!IsKindToken(expectedKind))
            {
                reference = null!;
                errorMessage = "Code kind must be a lowercase machine token using letters, digits, hyphens, underscores, and optional dot-separated segments.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            reference = null!;
            errorMessage = "Code must not be empty.";
            return false;
        }

        if (!UcliCodeValue.IsValidValue(code))
        {
            reference = null!;
            errorMessage = UcliCodeValue.InvalidValueMessage;
            return false;
        }

        reference = new CodeCatalogCodeReference(code, expectedKind);
        errorMessage = string.Empty;
        return true;
    }

    private static bool IsKindToken (string value)
    {
        var segmentStart = true;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (segmentStart)
            {
                if (!IsLowercaseAsciiLetter(character))
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

            if (!IsLowercaseAsciiLetter(character) && !IsAsciiDigit(character) && character != '-' && character != '_')
            {
                return false;
            }
        }

        return !segmentStart;
    }

    private static bool IsLowercaseAsciiLetter (char character)
    {
        return character >= 'a' && character <= 'z';
    }

    private static bool IsAsciiDigit (char character)
    {
        return character >= '0' && character <= '9';
    }
}
