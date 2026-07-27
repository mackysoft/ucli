using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Json;

/// <summary> Marks the runtime value serialized as one non-null JSON object. </summary>
internal interface IUcliNonNullJsonObject
{
}

/// <summary>
/// Carries one CLR value whose actual serializer contract is a non-null JSON object root.
/// </summary>
/// <typeparam name="T"> The authoritative serializer DTO type. </typeparam>
[JsonConverter(typeof(UcliNonNullJsonObjectJsonConverterFactory))]
internal readonly record struct UcliNonNullJsonObject<T> : IUcliNonNullJsonObject
{
    /// <summary> Initializes one non-null JSON object value. </summary>
    /// <param name="value"> The authoritative serializer DTO. </param>
    /// <exception cref="ArgumentNullException"> <paramref name="value" /> is <see langword="null" />. </exception>
    public UcliNonNullJsonObject (T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Value = value;
    }

    /// <summary> Gets the authoritative serializer DTO. </summary>
    internal T Value { get; }
}

/// <summary> Creates closed non-null JSON object values without duplicating their serializer DTOs. </summary>
internal static class UcliNonNullJsonObject
{
    private static readonly Type OpenValueType = typeof(UcliNonNullJsonObject<>);

    /// <summary> Wraps the exact runtime type serialized at a non-null JSON object boundary. </summary>
    /// <param name="value"> The source value. </param>
    /// <returns> A value-type serializer root that writes the source's unchanged JSON object shape. </returns>
    public static IUcliNonNullJsonObject Wrap (
        object value,
        JsonSerializerOptions serializerOptions)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (serializerOptions == null)
        {
            throw new ArgumentNullException(nameof(serializerOptions));
        }

        if (value is IUcliNonNullJsonObject wrapped)
        {
            return wrapped;
        }

        var serializerType = value.GetType();
        return Wrap(value, serializerType, serializerOptions);
    }

    /// <summary>
    /// Wraps a value through the declared serializer contract used at the JSON object boundary.
    /// </summary>
    public static IUcliNonNullJsonObject Wrap (
        object value,
        Type serializerType,
        JsonSerializerOptions serializerOptions)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (serializerType == null)
        {
            throw new ArgumentNullException(nameof(serializerType));
        }
        if (serializerOptions == null)
        {
            throw new ArgumentNullException(nameof(serializerOptions));
        }
        if (!serializerType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Value type '{value.GetType().FullName}' is not assignable to serializer contract '{serializerType.FullName}'.",
                nameof(value));
        }

        if (serializerOptions.GetTypeInfo(serializerType).Kind
            != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object)
        {
            throw new ArgumentException(
                $"The serializer contract for '{serializerType.FullName}' must produce a JSON object.",
                nameof(serializerType));
        }

        var valueType = OpenValueType.MakeGenericType(serializerType);
        return (IUcliNonNullJsonObject)(Activator.CreateInstance(valueType, value)
            ?? throw new InvalidOperationException(
                $"Could not create the non-null JSON object value for '{serializerType.FullName}'."));
    }

    /// <summary> Gets the closed wrapper type for one authoritative serializer DTO type. </summary>
    public static Type MakeValueType (Type serializerType)
    {
        if (serializerType == null)
        {
            throw new ArgumentNullException(nameof(serializerType));
        }

        return IsValueType(serializerType)
            ? serializerType
            : OpenValueType.MakeGenericType(serializerType);
    }

    internal static bool IsValueType (Type type)
    {
        return type.IsGenericType
            && type.GetGenericTypeDefinition() == OpenValueType;
    }

    internal static bool IsValueConverter (JsonConverter converter)
    {
        return converter is UcliNonNullJsonObjectJsonConverterFactory
            || converter.GetType().DeclaringType
                == typeof(UcliNonNullJsonObjectJsonConverterFactory);
    }
}

/// <summary>
/// Serializes <see cref="UcliNonNullJsonObject{T}" /> through the exact serializer contract of <typeparamref name="T" />.
/// </summary>
internal sealed class UcliNonNullJsonObjectJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert (Type typeToConvert)
    {
        return typeToConvert == typeof(IUcliNonNullJsonObject)
            || UcliNonNullJsonObject.IsValueType(typeToConvert);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter (
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert))
        {
            throw new ArgumentException(
                $"Type '{typeToConvert.FullName}' is not a non-null JSON object value.",
                nameof(typeToConvert));
        }

        if (typeToConvert == typeof(IUcliNonNullJsonObject))
        {
            return new InterfaceConverter();
        }

        var serializerType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(Converter<>).MakeGenericType(serializerType);
        return (JsonConverter)(Activator.CreateInstance(converterType)
            ?? throw new InvalidOperationException(
                $"Could not create the non-null JSON object converter for '{serializerType.FullName}'."));
    }

    private sealed class InterfaceConverter : JsonConverter<IUcliNonNullJsonObject>
    {
        public override IUcliNonNullJsonObject Read (
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException(
                "Non-null JSON object values do not support deserialization through the marker interface.");
        }

        public override void Write (
            Utf8JsonWriter writer,
            IUcliNonNullJsonObject value,
            JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new JsonException("A non-null JSON object value must not be null.");
            }

            var runtimeType = value.GetType();
            if (!UcliNonNullJsonObject.IsValueType(runtimeType))
            {
                throw new JsonException(
                    $"The runtime type '{runtimeType.FullName}' is not a non-null JSON object value.");
            }

            JsonSerializer.Serialize(writer, value, options.GetTypeInfo(runtimeType));
        }
    }

    private sealed class Converter<T> : JsonConverter<UcliNonNullJsonObject<T>>
    {
        public override UcliNonNullJsonObject<T> Read (
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(
                    $"The JSON value for '{typeof(T).FullName}' must be an object.");
            }

            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            if (value is null)
            {
                throw new JsonException(
                    $"The JSON object for '{typeof(T).FullName}' must not deserialize to null.");
            }

            return new UcliNonNullJsonObject<T>(value);
        }

        public override void Write (
            Utf8JsonWriter writer,
            UcliNonNullJsonObject<T> value,
            JsonSerializerOptions options)
        {
            if (value.Value is null)
            {
                throw new JsonException(
                    $"The JSON object for '{typeof(T).FullName}' must not be null.");
            }

            var typeInfo = options.GetTypeInfo(typeof(T));
            if (typeInfo.Kind != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object)
            {
                throw new JsonException(
                    $"The serializer contract for '{typeof(T).FullName}' must produce a JSON object.");
            }

            JsonSerializer.Serialize(writer, value.Value, typeInfo);
        }
    }
}
