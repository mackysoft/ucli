using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Base type for semantic string values that remain JSON strings on the IPC boundary. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public abstract record UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UcliStringValue" /> class. </summary>
    /// <param name="value"> The string value carried by the semantic contract type. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains only white-space characters, or contains malformed UTF-16 text.
    /// </exception>
    protected UcliStringValue (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Semantic string value must not be empty or whitespace.", nameof(value));
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            throw new ArgumentException("Semantic string value must not contain leading or trailing whitespace.", nameof(value));
        }

        ValidateWellFormedUtf16(value);
        Value = value;
    }

    /// <summary> Gets the underlying JSON string value. </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Value;
    }

    internal static bool IsAssignableFrom (Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return typeof(UcliStringValue).IsAssignableFrom(actualType);
    }

    private static void ValidateWellFormedUtf16 (string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
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

            throw new ArgumentException("Semantic string value must contain well-formed UTF-16 text.", nameof(value));
        }
    }
}
