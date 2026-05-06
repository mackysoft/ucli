using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts;

/// <summary> Represents a non-empty machine-readable error code from an open code set. </summary>
/// <remarks> Unknown values are preserved so new codes can be introduced without changing closed enum contracts. </remarks>
[JsonConverter(typeof(UcliErrorCodeJsonConverter))]
public readonly record struct UcliErrorCode
{
    /// <summary> Initializes a new instance of the <see cref="UcliErrorCode" /> struct. </summary>
    /// <param name="value"> The raw error code value. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is null, empty, or whitespace. </exception>
    public UcliErrorCode (string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException("Error code must not be null, empty, or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the raw error code value. </summary>
    public string Value { get; }

    /// <summary> Gets whether this instance contains a valid error code value. </summary>
    public bool IsValid => IsValidValue(Value);

    /// <summary> Converts one error code into its string value. </summary>
    /// <param name="code"> The error code to convert. </param>
    public static implicit operator string (UcliErrorCode code)
    {
        return code.Value ?? string.Empty;
    }

    /// <summary> Converts one string into an error code. </summary>
    /// <param name="value"> The raw error code value. </param>
    public static implicit operator UcliErrorCode (string value)
    {
        return new UcliErrorCode(value);
    }

    /// <summary> Tries to create one validated error code. </summary>
    /// <param name="value"> The raw error code value. </param>
    /// <param name="code"> The validated error code when successful. </param>
    /// <returns> <see langword="true" /> when the input is valid; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        out UcliErrorCode code)
    {
        if (!IsValidValue(value))
        {
            code = default;
            return false;
        }

        code = new UcliErrorCode(value!);
        return true;
    }

    /// <summary> Determines whether the specified value can be used as an error code. </summary>
    /// <param name="value"> The raw error code value. </param>
    /// <returns> <see langword="true" /> when the value is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidValue (string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value ?? string.Empty;
    }
}
