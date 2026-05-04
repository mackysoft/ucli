using System;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Base type for semantic string values that remain JSON strings on the IPC boundary. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public abstract record UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UcliStringValue" /> class. </summary>
    /// <param name="value"> The string value carried by the semantic contract type. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    protected UcliStringValue (string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary> Gets the underlying JSON string value. </summary>
    public string Value { get; }

    /// <summary> Converts a semantic string value to its underlying string. </summary>
    /// <param name="value"> The semantic string value. </param>
    /// <returns> The underlying string value, or <see langword="null" /> when <paramref name="value" /> is <see langword="null" />. </returns>
    public static implicit operator string? (UcliStringValue? value)
    {
        return value?.Value;
    }

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
}
