using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts semantic string value objects as JSON strings. </summary>
public sealed class UcliStringValueJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert (Type typeToConvert)
    {
        return UcliStringValue.IsAssignableFrom(typeToConvert);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter (
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert))
        {
            throw new ArgumentException("Type must derive from UcliStringValue.", nameof(typeToConvert));
        }

        var converterType = typeof(UcliStringValueJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class UcliStringValueJsonConverter<TValue> : JsonConverter<TValue>
        where TValue : UcliStringValue
    {
        private static readonly ConstructorInfo StringConstructor = typeof(TValue).GetConstructor(new[] { typeof(string) })
            ?? throw new InvalidOperationException($"Semantic string value type '{typeof(TValue).FullName}' must define a public string constructor.");

        public override TValue? Read (
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected a JSON string for '{typeToConvert.FullName}'.");
            }

            var value = reader.GetString() ?? throw new JsonException($"JSON string for '{typeToConvert.FullName}' must not be null.");
            return (TValue)StringConstructor.Invoke(new object[] { value });
        }

        public override void Write (
            Utf8JsonWriter writer,
            TValue value,
            JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value);
        }
    }
}
