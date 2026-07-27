using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Json;

/// <summary> Represents one immutable non-null JSON object value. </summary>
[JsonConverter(typeof(UcliJsonObjectJsonConverter))]
public readonly struct UcliJsonObject
{
    private readonly JsonElement value;

    /// <summary> Initializes an immutable copy of one JSON object. </summary>
    /// <param name="value"> The source JSON object. </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value" /> is not a JSON object.
    /// </exception>
    public UcliJsonObject (JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The JSON value must be an object.", nameof(value));
        }

        this.value = value.Clone();
    }

    /// <summary> Gets a value indicating whether this instance contains a JSON object. </summary>
    public bool IsDefined => value.ValueKind == JsonValueKind.Object;

    /// <summary> Gets the original JSON text for this object. </summary>
    /// <returns> The JSON text that represents this object. </returns>
    /// <exception cref="InvalidOperationException"> This instance does not contain a JSON object. </exception>
    public string GetRawText ()
    {
        EnsureDefined();
        return value.GetRawText();
    }

    /// <summary> Looks up one property by its exact JSON name. </summary>
    public bool TryGetProperty (
        string propertyName,
        out JsonElement property)
    {
        EnsureDefined();
        return value.TryGetProperty(propertyName, out property);
    }

    /// <summary> Writes this object to a JSON writer. </summary>
    /// <param name="writer"> The destination writer. </param>
    /// <exception cref="ArgumentNullException"> <paramref name="writer" /> is <see langword="null" />. </exception>
    /// <exception cref="InvalidOperationException"> This instance does not contain a JSON object. </exception>
    public void WriteTo (Utf8JsonWriter writer)
    {
        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        EnsureDefined();
        value.WriteTo(writer);
    }

    internal JsonElement ToJsonElement ()
    {
        EnsureDefined();
        return value.Clone();
    }

    private void EnsureDefined ()
    {
        if (!IsDefined)
        {
            throw new InvalidOperationException("The JSON object value is not initialized.");
        }
    }
}

/// <summary> Converts <see cref="UcliJsonObject" /> values as JSON objects. </summary>
public sealed class UcliJsonObjectJsonConverter : JsonConverter<UcliJsonObject>
{
    /// <inheritdoc />
    public override UcliJsonObject Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected a JSON object.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return new UcliJsonObject(document.RootElement);
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        UcliJsonObject value,
        JsonSerializerOptions options)
    {
        value.WriteTo(writer);
    }
}
