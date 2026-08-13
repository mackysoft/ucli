using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Json;

/// <summary> Represents the one public JSON literal <see langword="null" /> value. </summary>
[JsonConverter(typeof(UcliNullJsonConverter))]
public sealed class UcliNull
{
    /// <summary> Gets the sole marker used for a required literal-null property. </summary>
    public static UcliNull Value { get; } = new();

    private UcliNull () { }
}

/// <summary> Serializes the literal-null marker without widening its JSON contract. </summary>
public sealed class UcliNullJsonConverter : JsonConverter<UcliNull>
{
    /// <inheritdoc />
    public override UcliNull Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Null)
        {
            throw new JsonException("The value must be the JSON literal null.");
        }
        return UcliNull.Value;
    }

    /// <inheritdoc />
    public override void Write (Utf8JsonWriter writer, UcliNull value, JsonSerializerOptions options)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        writer.WriteNullValue();
    }
}
