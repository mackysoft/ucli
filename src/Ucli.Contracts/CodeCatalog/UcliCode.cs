using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts;

/// <summary> Represents one machine-readable code value from the shared uCLI open code set. </summary>
[JsonConverter(typeof(UcliCodeJsonConverter))]
public sealed record UcliCode
{
    /// <summary> Gets the maximum supported code value length. </summary>
    public const int MaximumLength = 128;

    /// <summary> Initializes a new code value after validating the open code-set syntax. </summary>
    /// <param name="value"> The uppercase ASCII code value to validate. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is null, empty, exceeds <see cref="MaximumLength" />, or contains a segment that does not start with an uppercase ASCII letter or contains a character other than an uppercase ASCII letter, digit, or underscore. </exception>
    public UcliCode (string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException(InvalidValueMessage, nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the raw code value. </summary>
    public string Value { get; }

    /// <summary> Gets the message used when a raw value violates the shared code value contract. </summary>
    public static string InvalidValueMessage => $"Code must be an uppercase machine token up to {MaximumLength} characters using letters, digits, underscores, and optional dot-separated segments.";

    /// <summary> Tries to create one validated uCLI code value. </summary>
    /// <param name="value"> The candidate code value. </param>
    /// <param name="code"> The validated code value when successful; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value is non-empty, does not exceed <see cref="MaximumLength" />, and every dot-delimited segment starts with an uppercase ASCII letter and otherwise contains only uppercase ASCII letters, digits, or underscores; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        [NotNullWhen(true)] out UcliCode? code)
    {
        if (!IsValidValue(value))
        {
            code = null;
            return false;
        }

        code = new UcliCode(value);
        return true;
    }

    /// <summary> Determines whether the specified value is a valid uCLI code value. </summary>
    /// <param name="value"> The candidate code value. </param>
    /// <returns> <see langword="true" /> when the value satisfies the length and dot-delimited segment constraints; otherwise <see langword="false" />. </returns>
    public static bool IsValidValue ([NotNullWhen(true)] string? value)
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

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
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
