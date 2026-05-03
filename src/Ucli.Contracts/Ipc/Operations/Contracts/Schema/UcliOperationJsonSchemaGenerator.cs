using System.Buffers;
using System.Globalization;
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
            if (GetDescriptionOrNull(property) == null)
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
            var context = new SchemaGenerationContext();
            WriteRootSchema(writer, contractType, context);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteRootSchema (
        Utf8JsonWriter writer,
        Type contractType,
        SchemaGenerationContext context)
    {
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (actualType == StringType || UcliStringValue.IsAssignableFrom(actualType))
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
            WriteSchemaReferenceOrInline(writer, elementType!, context);
            WriteDefinitionsIfNeeded(writer, context);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        context.PushActive(actualType);
        WriteObjectSchemaBody(writer, actualType, context);
        context.PopActive(actualType);
        WriteDefinitionsIfNeeded(writer, context);
        writer.WriteEndObject();
    }

    private static void WriteObjectSchemaBody (
        Utf8JsonWriter writer,
        Type contractType,
        SchemaGenerationContext context,
        bool includeNull = false)
    {
        var properties = UcliOperationContractReflection.GetSchemaProperties(contractType);
        WriteTypeProperty(writer, "object", includeNull);
        writer.WriteBoolean("additionalProperties", false);
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach (var property in properties)
        {
            writer.WritePropertyName(UcliOperationContractReflection.GetJsonPropertyName(property));
            WritePropertySchema(writer, property, context);
        }

        writer.WriteEndObject();
        WriteRequiredProperties(writer, properties);
    }

    private static void WritePropertySchema (
        Utf8JsonWriter writer,
        PropertyInfo property,
        SchemaGenerationContext context)
    {
        if (property.GetCustomAttribute<UcliJsonAnyValueAttribute>() != null)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();

        var propertyType = property.PropertyType;
        var nullableUnderlyingType = Nullable.GetUnderlyingType(propertyType);
        var actualType = nullableUnderlyingType ?? propertyType;
        var includeNull = nullableUnderlyingType != null || property.GetCustomAttribute<UcliJsonAllowNullAttribute>() != null;

        if (TryWriteScalarType(writer, actualType, includeNull))
        {
            writer.WriteEndObject();
            return;
        }

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            WriteTypeProperty(writer, "array", includeNull);
            writer.WritePropertyName("items");
            WriteSchemaReferenceOrInline(writer, elementType!, context);
            writer.WriteEndObject();
            return;
        }

        if (context.IsActive(actualType))
        {
            WriteObjectReference(writer, actualType, context);
        }
        else
        {
            context.PushActive(actualType);
            WriteObjectSchemaBody(writer, actualType, context, includeNull);
            context.PopActive(actualType);
        }

        writer.WriteEndObject();
    }

    private static void WriteSchemaReferenceOrInline (
        Utf8JsonWriter writer,
        Type contractType,
        SchemaGenerationContext context)
    {
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (actualType == StringType || UcliStringValue.IsAssignableFrom(actualType))
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
            WriteTypeProperty(writer, "array", includeNull: false);
            writer.WritePropertyName("items");
            WriteSchemaReferenceOrInline(writer, elementType!, context);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        WriteObjectReference(writer, actualType, context);
        writer.WriteEndObject();
    }

    private static void WriteObjectReference (
        Utf8JsonWriter writer,
        Type contractType,
        SchemaGenerationContext context)
    {
        var definitionName = context.GetOrAddDefinition(contractType);
        writer.WriteString("$ref", "#/$defs/" + definitionName);
    }

    private static void WriteDefinitionsIfNeeded (
        Utf8JsonWriter writer,
        SchemaGenerationContext context)
    {
        if (context.DefinitionCount == 0)
        {
            return;
        }

        writer.WritePropertyName("$defs");
        writer.WriteStartObject();
        var index = 0;
        while (index < context.DefinitionCount)
        {
            var definition = context.GetDefinition(index);
            writer.WritePropertyName(definition.Name);
            writer.WriteStartObject();
            context.PushActive(definition.Type);
            WriteObjectSchemaBody(writer, definition.Type, context, includeNull: false);
            context.PopActive(definition.Type);
            writer.WriteEndObject();
            index++;
        }

        writer.WriteEndObject();
    }

    private static bool TryWriteScalarType (
        Utf8JsonWriter writer,
        Type actualType,
        bool includeNull)
    {
        string? schemaType = null;
        if (actualType == StringType || UcliStringValue.IsAssignableFrom(actualType))
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

        WriteTypeProperty(writer, schemaType, includeNull);

        return true;
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

    private static void WriteType (
        Utf8JsonWriter writer,
        string schemaType)
    {
        writer.WriteStartObject();
        WriteTypeProperty(writer, schemaType, includeNull: false);
        writer.WriteEndObject();
    }

    private static void WriteTypeProperty (
        Utf8JsonWriter writer,
        string schemaType,
        bool includeNull)
    {
        if (includeNull)
        {
            writer.WritePropertyName("type");
            writer.WriteStartArray();
            writer.WriteStringValue(schemaType);
            writer.WriteStringValue("null");
            writer.WriteEndArray();
            return;
        }

        writer.WriteString("type", schemaType);
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

    private static string? GetDescriptionOrNull (PropertyInfo property)
    {
        var description = property.GetCustomAttribute<UcliDescriptionAttribute>();
        if (description != null)
        {
            return description.Description;
        }

        var actualType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return UcliStringValue.IsAssignableFrom(actualType)
            ? actualType.GetCustomAttribute<UcliDescriptionAttribute>()?.Description
            : null;
    }

    private sealed class SchemaGenerationContext
    {
        private readonly Dictionary<Type, string> definitionNames = new Dictionary<Type, string>();

        private readonly List<SchemaDefinition> definitions = new List<SchemaDefinition>();

        private readonly HashSet<Type> activeTypes = new HashSet<Type>();

        private readonly HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);

        public int DefinitionCount => definitions.Count;

        public string GetOrAddDefinition (Type type)
        {
            if (definitionNames.TryGetValue(type, out var existingName))
            {
                return existingName;
            }

            var definitionName = CreateUniqueDefinitionName(type);
            definitionNames.Add(type, definitionName);
            definitions.Add(new SchemaDefinition(definitionName, type));
            return definitionName;
        }

        public SchemaDefinition GetDefinition (int index)
        {
            return definitions[index];
        }

        public bool IsActive (Type type)
        {
            return activeTypes.Contains(type);
        }

        public void PushActive (Type type)
        {
            activeTypes.Add(type);
        }

        public void PopActive (Type type)
        {
            activeTypes.Remove(type);
        }

        private string CreateUniqueDefinitionName (Type type)
        {
            var baseName = type.Name;
            var tickIndex = baseName.IndexOf('`');
            if (tickIndex >= 0)
            {
                baseName = baseName.Substring(0, tickIndex);
            }

            var candidate = baseName;
            var suffix = 2;
            while (!usedNames.Add(candidate))
            {
                candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            return candidate;
        }
    }

    private readonly struct SchemaDefinition
    {
        public SchemaDefinition (string name, Type type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }

        public Type Type { get; }
    }
}
