namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Represents a non-empty machine-readable SKILL failure code from an open code set. </summary>
/// <remarks> Unknown values are preserved so new SKILL failure codes can be introduced without closed enum changes. </remarks>
public readonly record struct SkillFailureCode
{
    /// <summary> Initializes a new instance of the <see cref="SkillFailureCode" /> struct. </summary>
    /// <param name="value"> The raw failure code value. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is null, empty, or whitespace. </exception>
    public SkillFailureCode (string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException("Failure code must not be null, empty, or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the raw failure code value. </summary>
    public string Value { get; }

    /// <summary> Gets whether this instance contains a valid failure code value. </summary>
    public bool IsValid => IsValidValue(Value);

    /// <summary> Tries to create one validated failure code. </summary>
    /// <param name="value"> The raw failure code value. </param>
    /// <param name="code"> The validated failure code when successful. </param>
    /// <returns> <see langword="true" /> when the input is valid; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out SkillFailureCode code)
    {
        if (!IsValidValue(value))
        {
            code = default;
            return false;
        }

        code = new SkillFailureCode(value!);
        return true;
    }

    /// <summary> Determines whether the specified value can be used as a SKILL failure code. </summary>
    /// <param name="value"> The raw failure code value. </param>
    /// <returns> <see langword="true" /> when the value is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidValue (string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary> Converts the failure code to its raw string value. </summary>
    /// <param name="code"> The failure code to convert. </param>
    public static implicit operator string (SkillFailureCode code)
    {
        return code.ToString();
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value ?? string.Empty;
    }
}
