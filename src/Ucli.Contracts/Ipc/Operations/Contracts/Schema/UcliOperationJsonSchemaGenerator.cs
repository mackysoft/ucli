using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Operations;

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
        foreach (var property in UcliOperationSchemaPropertySelector.GetSchemaProperties(contractType))
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
        if (TryWriteKnownSchemaObject(writer, actualType, includeNull: false))
        {
            return;
        }

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            WriteRootArraySchema(writer, elementType!, context);
            return;
        }

        WriteRootObjectSchema(writer, actualType, context);
    }

    private static void WriteObjectSchemaBody (
        Utf8JsonWriter writer,
        Type contractType,
        SchemaGenerationContext context,
        bool includeNull = false)
    {
        WriteTypeProperty(writer, "object", includeNull);
        writer.WriteBoolean("additionalProperties", false);
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        var properties = UcliOperationSchemaPropertySelector.GetSchemaProperties(contractType);
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

        var propertyType = SchemaPropertyType.Create(property);
        if (TryWriteKnownSchemaBody(writer, propertyType.ActualType, propertyType.IncludeNull))
        {
            writer.WriteEndObject();
            return;
        }

        if (TryGetArrayElementType(propertyType.ActualType, out var elementType))
        {
            WriteArraySchemaBody(writer, elementType!, context, propertyType.IncludeNull);
            writer.WriteEndObject();
            return;
        }

        if (context.IsActive(propertyType.ActualType))
        {
            WriteObjectReference(writer, propertyType.ActualType, context);
        }
        else
        {
            context.PushActive(propertyType.ActualType);
            WriteObjectSchemaBody(writer, propertyType.ActualType, context, propertyType.IncludeNull);
            context.PopActive(propertyType.ActualType);
        }

        writer.WriteEndObject();
    }

    private static void WriteSchemaReferenceOrInline (
        Utf8JsonWriter writer,
        Type contractType,
        SchemaGenerationContext context)
    {
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (TryWriteKnownSchemaObject(writer, actualType, includeNull: false))
        {
            return;
        }

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            WriteArraySchemaObject(writer, elementType!, context, includeNull: false);
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

    private static void WriteRootObjectSchema (
        Utf8JsonWriter writer,
        Type actualType,
        SchemaGenerationContext context)
    {
        writer.WriteStartObject();
        context.PushActive(actualType);
        WriteObjectSchemaBody(writer, actualType, context);
        context.PopActive(actualType);
        WriteDefinitionsIfNeeded(writer, context);
        writer.WriteEndObject();
    }

    private static void WriteRootArraySchema (
        Utf8JsonWriter writer,
        Type elementType,
        SchemaGenerationContext context)
    {
        writer.WriteStartObject();
        WriteArraySchemaBody(writer, elementType, context, includeNull: false);
        WriteDefinitionsIfNeeded(writer, context);
        writer.WriteEndObject();
    }

    private static void WriteArraySchemaObject (
        Utf8JsonWriter writer,
        Type elementType,
        SchemaGenerationContext context,
        bool includeNull)
    {
        writer.WriteStartObject();
        WriteArraySchemaBody(writer, elementType, context, includeNull);
        writer.WriteEndObject();
    }

    private static void WriteArraySchemaBody (
        Utf8JsonWriter writer,
        Type elementType,
        SchemaGenerationContext context,
        bool includeNull)
    {
        WriteTypeProperty(writer, "array", includeNull);
        writer.WritePropertyName("items");
        WriteSchemaReferenceOrInline(writer, elementType, context);
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

    private static bool TryWriteKnownSchemaObject (
        Utf8JsonWriter writer,
        Type actualType,
        bool includeNull)
    {
        if (!CanWriteKnownSchema(actualType))
        {
            return false;
        }

        writer.WriteStartObject();
        TryWriteKnownSchemaBody(writer, actualType, includeNull);
        writer.WriteEndObject();
        return true;
    }

    private static bool TryWriteKnownSchemaBody (
        Utf8JsonWriter writer,
        Type actualType,
        bool includeNull)
    {
        if (TryGetSchemaType(actualType, out var schemaType))
        {
            WriteTypeProperty(writer, schemaType!, includeNull);
            return true;
        }

        return actualType == JsonElementType || actualType == typeof(object);
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

    private static bool CanWriteKnownSchema (Type actualType)
    {
        return TryGetSchemaType(actualType, out _)
            || actualType == JsonElementType
            || actualType == typeof(object);
    }

    private static bool TryGetSchemaType (
        Type actualType,
        out string? schemaType)
    {
        if (actualType == StringType || UcliStringValue.IsAssignableFrom(actualType) || actualType.IsEnum)
        {
            schemaType = "string";
            return true;
        }

        if (actualType == typeof(bool))
        {
            schemaType = "boolean";
            return true;
        }

        return TryGetNumberSchemaType(actualType, out schemaType);
    }

    private static bool TryGetNumberSchemaType (
        Type actualType,
        out string? schemaType)
    {
        if (IsInteger(actualType))
        {
            schemaType = "integer";
            return true;
        }

        if (IsNumber(actualType))
        {
            schemaType = "number";
            return true;
        }

        schemaType = null;
        return false;
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

    private readonly struct SchemaPropertyType
    {
        private SchemaPropertyType (
            Type actualType,
            bool includeNull)
        {
            ActualType = actualType;
            IncludeNull = includeNull;
        }

        public Type ActualType { get; }

        public bool IncludeNull { get; }

        public static SchemaPropertyType Create (PropertyInfo property)
        {
            var nullableUnderlyingType = Nullable.GetUnderlyingType(property.PropertyType);
            var actualType = nullableUnderlyingType ?? property.PropertyType;
            var includeNull = nullableUnderlyingType != null
                || property.GetCustomAttribute<UcliJsonAllowNullAttribute>() != null;
            return new SchemaPropertyType(actualType, includeNull);
        }
    }
}
