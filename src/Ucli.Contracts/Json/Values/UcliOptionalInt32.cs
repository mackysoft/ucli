using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Json;

/// <summary>Represents an omitted JSON property or one explicitly supplied 32-bit integer.</summary>
[JsonConverter(typeof(UcliOptionalInt32JsonConverter))]
internal readonly record struct UcliOptionalInt32
{
    private readonly int value;

    private UcliOptionalInt32 (int value)
    {
        this.value = value;
        HasValue = true;
    }

    /// <summary>Gets whether the JSON property was supplied.</summary>
    public bool HasValue { get; }

    /// <summary>Gets the supplied integer.</summary>
    /// <exception cref="InvalidOperationException">The JSON property was omitted.</exception>
    public int Value => HasValue
        ? value
        : throw new InvalidOperationException("The optional JSON integer was omitted.");

    /// <summary>Creates an explicitly supplied integer value.</summary>
    public static UcliOptionalInt32 FromValue (int value) => new(value);
}

/// <summary>Serializes a supplied optional integer as a JSON number and rejects JSON null.</summary>
internal sealed class UcliOptionalInt32JsonConverter : JsonConverter<UcliOptionalInt32>
{
    public override UcliOptionalInt32 Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value))
        {
            throw new JsonException("An optional 32-bit integer must be a JSON integer when supplied.");
        }

        return UcliOptionalInt32.FromValue(value);
    }

    public override void Write (
        Utf8JsonWriter writer,
        UcliOptionalInt32 value,
        JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            throw new JsonException("An omitted optional integer must be omitted by its declaring object.");
        }

        writer.WriteNumberValue(value.Value);
    }
}
