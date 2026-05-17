namespace MackySoft.Ucli.Contracts;

/// <summary> Represents one machine-readable code value from the shared uCLI open code set. </summary>
public readonly record struct UcliCodeValue
{
    /// <summary> Gets the maximum supported code value length. </summary>
    public const int MaximumLength = 128;

    /// <summary> Initializes a new instance of the <see cref="UcliCodeValue" /> struct. </summary>
    /// <param name="value"> The raw code value. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a valid uCLI code value. </exception>
    public UcliCodeValue (string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException(InvalidValueMessage, nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the raw code value. </summary>
    public string Value { get; }

    /// <summary> Gets whether this instance contains a valid uCLI code value. </summary>
    public bool IsValid => IsValidValue(Value);

    /// <summary> Gets the message used when a raw value violates the shared code value contract. </summary>
    public static string InvalidValueMessage => $"Code must be an uppercase machine token up to {MaximumLength} characters using letters, digits, underscores, and optional dot-separated segments.";

    /// <summary> Tries to create one validated uCLI code value. </summary>
    /// <param name="value"> The raw code value. </param>
    /// <param name="code"> The validated code value when successful. </param>
    /// <returns> <see langword="true" /> when the input is valid; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out UcliCodeValue code)
    {
        if (!IsValidValue(value))
        {
            code = default;
            return false;
        }

        code = new UcliCodeValue(value!);
        return true;
    }

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

    /// <summary> Determines whether this code value equals the specified raw value. </summary>
    /// <param name="value"> The raw code value to compare. </param>
    /// <returns> <see langword="true" /> when the raw value equals this code value. </returns>
    public bool EqualsValue (string? value)
    {
        return string.Equals(Value, value, StringComparison.Ordinal);
    }

    /// <summary> Converts the code value to its raw string value. </summary>
    /// <param name="code"> The code value to convert. </param>
    public static implicit operator string (UcliCodeValue code)
    {
        return code.ToString();
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value ?? string.Empty;
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
