using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Generates the JSON Schema subset used by uCLI operation args and result contracts. </summary>
public static class UcliOperationJsonSchemaGenerator
{
    private static readonly Type StringType = typeof(string);

    private static readonly Type JsonElementType = typeof(JsonElement);

    /// <summary> Creates one JSON Schema object text for an operation argument contract type. </summary>
    /// <param name="contractType"> The operation argument contract type. </param>
    /// <returns> The generated JSON Schema object text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractType" /> is <see langword="null" />. </exception>
    public static string CreateArgsSchemaJson (Type contractType)
    {
        if (contractType == null)
        {
            throw new ArgumentNullException(nameof(contractType));
        }

        return CreateSchemaJson(contractType);
    }

    /// <summary> Creates one JSON Schema object text for an operation result contract type. </summary>
    /// <param name="contractType"> The operation result contract type. </param>
    /// <returns> The generated JSON Schema object text, or <see langword="null" /> when no result payload is emitted. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractType" /> is <see langword="null" />. </exception>
    public static string? CreateResultSchemaJson (Type contractType)
    {
        if (contractType == null)
        {
            throw new ArgumentNullException(nameof(contractType));
        }

        return contractType == typeof(UcliNoResult) ? null : CreateSchemaJson(contractType);
    }

    /// <summary> Gets public contract properties that do not have schema descriptions. </summary>
    /// <param name="contractType"> The contract type to inspect. </param>
    /// <returns> The public JSON property names whose descriptions are missing. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractType" /> is <see langword="null" />. </exception>
    public static IReadOnlyList<string> FindMissingPropertyDescriptions (Type contractType)
    {
        if (contractType == null)
        {
            throw new ArgumentNullException(nameof(contractType));
        }

        var missing = new List<string>();
        foreach (var property in UcliOperationContractReflection.GetSchemaProperties(contractType))
        {
            if (property.GetCustomAttribute<UcliDescriptionAttribute>() == null)
            {
                missing.Add(UcliOperationContractReflection.GetJsonPropertyName(property));
            }
        }

        return missing;
    }

    private static string CreateSchemaJson (Type contractType)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteSchema(writer, contractType, new HashSet<Type>());
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteSchema (
        Utf8JsonWriter writer,
        Type contractType,
        HashSet<Type> visitedTypes)
    {
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (actualType == StringType)
        {
            WriteType(writer, "string");
            return;
        }

        if (actualType == typeof(bool))
        {
            WriteType(writer, "boolean");
            return;
        }

        if (IsInteger(actualType))
        {
            WriteType(writer, "integer");
            return;
        }

        if (IsNumber(actualType))
        {
            WriteType(writer, "number");
            return;
        }

        if (actualType == JsonElementType || actualType == typeof(object))
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "array");
            writer.WritePropertyName("items");
            WriteSchema(writer, elementType!, visitedTypes);
            writer.WriteEndObject();
            return;
        }

        WriteObjectSchema(writer, actualType, visitedTypes);
    }

    private static void WriteObjectSchema (
        Utf8JsonWriter writer,
        Type contractType,
        HashSet<Type> visitedTypes)
    {
        if (!visitedTypes.Add(contractType))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteBoolean("additionalProperties", true);
            writer.WriteEndObject();
            return;
        }

        var properties = UcliOperationContractReflection.GetSchemaProperties(contractType);
        writer.WriteStartObject();
        writer.WriteString("type", "object");
        var description = contractType.GetCustomAttribute<UcliDescriptionAttribute>();
        if (description != null)
        {
            writer.WriteString("description", description.Description);
        }

        writer.WriteBoolean("additionalProperties", false);
        var minProperties = contractType.GetCustomAttribute<UcliMinPropertiesAttribute>();
        if (minProperties != null)
        {
            writer.WriteNumber("minProperties", minProperties.MinProperties);
        }

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach (var property in properties)
        {
            writer.WritePropertyName(UcliOperationContractReflection.GetJsonPropertyName(property));
            WritePropertySchema(writer, property, visitedTypes);
        }

        writer.WriteEndObject();
        WriteRequiredProperties(writer, properties);
        WriteOneOf(writer, contractType);
        WriteConditionalRules(writer, contractType);
        writer.WriteEndObject();
        visitedTypes.Remove(contractType);
    }

    private static void WritePropertySchema (
        Utf8JsonWriter writer,
        PropertyInfo property,
        HashSet<Type> visitedTypes)
    {
        if (property.GetCustomAttribute<UcliSchemaAnyAttribute>() != null)
        {
            writer.WriteStartObject();
            WriteDescription(writer, property);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        WriteDescription(writer, property);

        var propertyType = property.PropertyType;
        var nullableUnderlyingType = Nullable.GetUnderlyingType(propertyType);
        var actualType = nullableUnderlyingType ?? propertyType;
        var includeNull = nullableUnderlyingType != null || property.GetCustomAttribute<UcliNullableAttribute>() != null;

        if (TryWriteScalarType(writer, actualType, includeNull))
        {
            WritePropertyConstraints(writer, property);
            writer.WriteEndObject();
            return;
        }

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            writer.WriteString("type", "array");
            var minItems = property.GetCustomAttribute<UcliMinItemsAttribute>();
            if (minItems != null)
            {
                writer.WriteNumber("minItems", minItems.MinItems);
            }

            writer.WritePropertyName("items");
            WriteSchema(writer, elementType!, visitedTypes);
            writer.WriteEndObject();
            return;
        }

        WriteNestedObjectSchema(writer, actualType, visitedTypes);
        writer.WriteEndObject();
    }

    private static void WriteNestedObjectSchema (
        Utf8JsonWriter writer,
        Type contractType,
        HashSet<Type> visitedTypes)
    {
        if (!visitedTypes.Add(contractType))
        {
            writer.WriteString("type", "object");
            writer.WriteBoolean("additionalProperties", true);
            return;
        }

        var properties = UcliOperationContractReflection.GetSchemaProperties(contractType);
        writer.WriteString("type", "object");
        writer.WriteBoolean("additionalProperties", false);
        var minProperties = contractType.GetCustomAttribute<UcliMinPropertiesAttribute>();
        if (minProperties != null)
        {
            writer.WriteNumber("minProperties", minProperties.MinProperties);
        }

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach (var nestedProperty in properties)
        {
            writer.WritePropertyName(UcliOperationContractReflection.GetJsonPropertyName(nestedProperty));
            WritePropertySchema(writer, nestedProperty, visitedTypes);
        }

        writer.WriteEndObject();
        WriteRequiredProperties(writer, properties);
        WriteOneOf(writer, contractType);
        WriteConditionalRules(writer, contractType);
        visitedTypes.Remove(contractType);
    }

    private static bool TryWriteScalarType (
        Utf8JsonWriter writer,
        Type actualType,
        bool includeNull)
    {
        string? schemaType = null;
        if (actualType == StringType)
        {
            schemaType = "string";
        }
        else if (actualType == typeof(bool))
        {
            schemaType = "boolean";
        }
        else if (IsInteger(actualType))
        {
            schemaType = "integer";
        }
        else if (IsNumber(actualType))
        {
            schemaType = "number";
        }

        if (schemaType == null)
        {
            if (actualType == JsonElementType || actualType == typeof(object))
            {
                return true;
            }

            return false;
        }

        if (includeNull)
        {
            writer.WritePropertyName("type");
            writer.WriteStartArray();
            writer.WriteStringValue(schemaType);
            writer.WriteStringValue("null");
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteString("type", schemaType);
        }

        return true;
    }

    private static void WritePropertyConstraints (
        Utf8JsonWriter writer,
        PropertyInfo property)
    {
        var minLength = property.GetCustomAttribute<UcliMinLengthAttribute>();
        if (minLength != null)
        {
            writer.WriteNumber("minLength", minLength.MinLength);
        }

        var minimum = property.GetCustomAttribute<UcliMinimumAttribute>();
        if (minimum != null)
        {
            writer.WriteNumber("minimum", minimum.Minimum);
        }
    }

    private static void WriteDescription (
        Utf8JsonWriter writer,
        PropertyInfo property)
    {
        var description = property.GetCustomAttribute<UcliDescriptionAttribute>();
        if (description != null)
        {
            writer.WriteString("description", description.Description);
        }
    }

    private static void WriteRequiredProperties (
        Utf8JsonWriter writer,
        IReadOnlyList<PropertyInfo> properties)
    {
        var requiredProperties = new List<string>();
        foreach (var property in properties)
        {
            if (property.GetCustomAttribute<UcliRequiredAttribute>() != null)
            {
                requiredProperties.Add(UcliOperationContractReflection.GetJsonPropertyName(property));
            }
        }

        if (requiredProperties.Count == 0)
        {
            return;
        }

        writer.WritePropertyName("required");
        writer.WriteStartArray();
        for (var i = 0; i < requiredProperties.Count; i++)
        {
            writer.WriteStringValue(requiredProperties[i]);
        }

        writer.WriteEndArray();
    }

    private static void WriteOneOf (
        Utf8JsonWriter writer,
        Type contractType)
    {
        var alternatives = contractType.GetCustomAttributes<UcliOneOfRequiredAttribute>().ToArray();
        if (alternatives.Length == 0)
        {
            return;
        }

        writer.WritePropertyName("oneOf");
        writer.WriteStartArray();
        for (var i = 0; i < alternatives.Length; i++)
        {
            WriteRequiredAlternative(writer, alternatives[i].PropertyNames);
        }

        writer.WriteEndArray();
    }

    private static void WriteConditionalRules (
        Utf8JsonWriter writer,
        Type contractType)
    {
        var conditionalRules = contractType.GetCustomAttributes<UcliIfRequiredThenOneOfRequiredAttribute>().ToArray();
        if (conditionalRules.Length == 0)
        {
            return;
        }

        writer.WritePropertyName("allOf");
        writer.WriteStartArray();
        for (var i = 0; i < conditionalRules.Length; i++)
        {
            var rule = conditionalRules[i];
            writer.WriteStartObject();
            writer.WritePropertyName("if");
            WriteRequiredAlternative(writer, new[] { rule.ConditionPropertyName });
            writer.WritePropertyName("then");
            writer.WriteStartObject();
            writer.WritePropertyName("oneOf");
            writer.WriteStartArray();
            WriteRequiredAlternative(writer, rule.ThenPropertyNames);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteRequiredAlternative (
        Utf8JsonWriter writer,
        IReadOnlyList<string> propertyNames)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("required");
        writer.WriteStartArray();
        for (var i = 0; i < propertyNames.Count; i++)
        {
            writer.WriteStringValue(propertyNames[i]);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteType (
        Utf8JsonWriter writer,
        string schemaType)
    {
        writer.WriteStartObject();
        writer.WriteString("type", schemaType);
        writer.WriteEndObject();
    }

    private static bool TryGetArrayElementType (
        Type type,
        out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType != null;
        }

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(IReadOnlyList<>)
                || genericTypeDefinition == typeof(IReadOnlyCollection<>)
                || genericTypeDefinition == typeof(IEnumerable<>)
                || genericTypeDefinition == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private static bool IsInteger (Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong);
    }

    private static bool IsNumber (Type type)
    {
        return type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }
}
