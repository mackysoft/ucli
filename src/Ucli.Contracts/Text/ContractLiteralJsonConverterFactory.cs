using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Creates strict JSON converters for enum types backed by uCLI contract literals. </summary>
/// <remarks>
/// Once one member declares <see cref="UcliContractLiteralAttribute" />, every declared member must be
/// part of the literal contract. Invalid definitions are rejected while the serializer resolves the converter.
/// </remarks>
public sealed class ContractLiteralJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException"> Thrown when a recognized enum has an incomplete or invalid literal definition. </exception>
    public override bool CanConvert (Type typeToConvert)
    {
        if (!typeToConvert.IsEnum)
        {
            return false;
        }

        var hasContractLiteral = typeToConvert
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Any(static field => field.IsDefined(typeof(UcliContractLiteralAttribute), inherit: false));
        if (hasContractLiteral)
        {
            ContractLiteralCodec.Validate(typeToConvert);
        }

        return hasContractLiteral;
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter (
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert))
        {
            throw new ArgumentException(
                $"Type '{typeToConvert.FullName}' is not an enum backed by uCLI contract literals.",
                nameof(typeToConvert));
        }

        return (JsonConverter)Activator.CreateInstance(
            typeof(ContractLiteralJsonConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class ContractLiteralJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read (
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"{typeof(TEnum).Name} must be a string contract literal.");
            }

            var literal = reader.GetString();
            if (!ContractLiteralCodec.TryParse(literal, out TEnum value))
            {
                throw new JsonException($"Unsupported {typeof(TEnum).Name} contract literal '{literal}'.");
            }

            return value;
        }

        public override TEnum ReadAsPropertyName (
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"{typeof(TEnum).Name} dictionary key must be a string contract literal.");
            }

            var literal = reader.GetString();
            if (!ContractLiteralCodec.TryParse(literal, out TEnum value))
            {
                throw new JsonException($"Unsupported {typeof(TEnum).Name} contract-literal property name '{literal}'.");
            }

            return value;
        }

        public override void Write (
            Utf8JsonWriter writer,
            TEnum value,
            JsonSerializerOptions options)
        {
            if (!ContractLiteralCodec.TryToValue(value, out var literal))
            {
                throw new JsonException($"Unsupported {typeof(TEnum).Name} value '{value}'.");
            }

            writer.WriteStringValue(literal);
        }

        public override void WriteAsPropertyName (
            Utf8JsonWriter writer,
            TEnum value,
            JsonSerializerOptions options)
        {
            if (!ContractLiteralCodec.TryToValue(value, out var literal))
            {
                throw new JsonException($"Unsupported {typeof(TEnum).Name} value '{value}'.");
            }

            writer.WritePropertyName(literal);
        }
    }
}
