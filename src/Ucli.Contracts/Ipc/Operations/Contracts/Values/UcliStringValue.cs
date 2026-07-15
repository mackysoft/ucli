using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Base type for semantic string values that remain JSON strings on the IPC boundary. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public abstract class UcliStringValue : IEquatable<UcliStringValue>
{
    /// <summary> Initializes a new instance of the <see cref="UcliStringValue" /> class. </summary>
    /// <param name="value"> The string value carried by the semantic contract type. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains only white-space characters, has outer whitespace, or contains malformed UTF-16 text.
    /// </exception>
    protected UcliStringValue (string value)
    {
        Value = ValidateStringOrThrow(value);
    }

    /// <summary> Gets the underlying JSON string value. </summary>
    public string Value { get; }

    /// <inheritdoc />
    public bool Equals (UcliStringValue? other)
    {
        return ReferenceEquals(this, other)
            || (other is not null
                && GetType() == other.GetType()
                && string.Equals(Value, other.Value, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public sealed override bool Equals (object? obj)
    {
        return obj is UcliStringValue other && Equals(other);
    }

    /// <inheritdoc />
    public sealed override int GetHashCode ()
    {
        return HashCode.Combine(GetType(), StringComparer.Ordinal.GetHashCode(Value));
    }

    /// <summary> Determines whether two semantic string values have the same runtime type and value. </summary>
    public static bool operator == (UcliStringValue? left, UcliStringValue? right)
    {
        return left is null
            ? right is null
            : left.Equals(right);
    }

    /// <summary> Determines whether two semantic string values have a different runtime type or value. </summary>
    public static bool operator != (UcliStringValue? left, UcliStringValue? right)
    {
        return !(left == right);
    }

    /// <inheritdoc />
    public sealed override string ToString ()
    {
        return Value;
    }

    internal static bool IsAssignableFrom (Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return typeof(UcliStringValue).IsAssignableFrom(actualType);
    }

    /// <summary> Validates the common semantic string invariants without throwing. </summary>
    /// <param name="value"> The candidate string value. </param>
    /// <returns> <see langword="true" /> when the common invariants are satisfied; otherwise <see langword="false" />. </returns>
    private protected static bool IsValidString (string? value)
    {
        return ValidateString(value) == StringValidationFailure.None;
    }

    /// <summary> Validates the common semantic string invariants and returns a validated construction value. </summary>
    /// <param name="value"> The candidate string value. </param>
    /// <returns> The validated string value. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains only white-space characters, has outer whitespace, or contains malformed UTF-16 text.
    /// </exception>
    private static string ValidateStringOrThrow (string? value)
    {
        var failure = ValidateString(value);
        return failure switch
        {
            StringValidationFailure.None => value!,
            StringValidationFailure.Null => throw new ArgumentNullException(nameof(value)),
            StringValidationFailure.EmptyOrWhitespace => throw new ArgumentException(
                "Semantic string value must not be empty or whitespace.",
                nameof(value)),
            StringValidationFailure.OuterWhitespace => throw new ArgumentException(
                "Semantic string value must not contain leading or trailing whitespace.",
                nameof(value)),
            StringValidationFailure.MalformedUtf16 => throw new ArgumentException(
                "Semantic string value must contain well-formed UTF-16 text.",
                nameof(value)),
            _ => throw new InvalidOperationException($"Unknown semantic string validation failure '{failure}'."),
        };
    }

    private static StringValidationFailure ValidateString (string? value)
    {
        if (value == null)
        {
            return StringValidationFailure.Null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return StringValidationFailure.EmptyOrWhitespace;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            return StringValidationFailure.OuterWhitespace;
        }

        if (!StringValueValidator.IsWellFormedUtf16(value))
        {
            return StringValidationFailure.MalformedUtf16;
        }

        return StringValidationFailure.None;
    }

    private enum StringValidationFailure
    {
        None,
        Null,
        EmptyOrWhitespace,
        OuterWhitespace,
        MalformedUtf16,
    }
}
