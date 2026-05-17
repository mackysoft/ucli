namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Represents one non-empty machine-readable assurance claim code. </summary>
internal readonly record struct AssuranceClaimCode
{
    /// <summary> Initializes a new instance of the <see cref="AssuranceClaimCode" /> struct. </summary>
    /// <param name="value"> The raw claim code value. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is null, empty, or whitespace. </exception>
    public AssuranceClaimCode (string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException("Assurance claim code must not be null, empty, or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the raw claim code value. </summary>
    public string Value { get; }

    /// <summary> Gets whether this instance contains a valid claim code value. </summary>
    public bool IsValid => IsValidValue(Value);

    /// <summary> Tries to create one validated claim code. </summary>
    /// <param name="value"> The raw claim code value. </param>
    /// <param name="code"> The validated claim code when successful. </param>
    /// <returns> <see langword="true" /> when the input is valid; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out AssuranceClaimCode code)
    {
        if (!IsValidValue(value))
        {
            code = default;
            return false;
        }

        code = new AssuranceClaimCode(value!);
        return true;
    }

    /// <summary> Determines whether the specified value can be used as a claim code. </summary>
    /// <param name="value"> The raw claim code value. </param>
    /// <returns> <see langword="true" /> when the value is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidValue (string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary> Determines whether this claim code equals the specified raw value. </summary>
    /// <param name="value"> The raw claim code value to compare. </param>
    /// <returns> <see langword="true" /> when the raw value equals this claim code. </returns>
    public bool EqualsValue (string? value)
    {
        return string.Equals(Value, value, StringComparison.Ordinal);
    }

    /// <summary> Converts the claim code to its raw string value. </summary>
    /// <param name="code"> The claim code to convert. </param>
    public static implicit operator string (AssuranceClaimCode code)
    {
        return code.ToString();
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value ?? string.Empty;
    }
}
