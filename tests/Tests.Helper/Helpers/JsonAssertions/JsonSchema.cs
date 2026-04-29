namespace MackySoft.Tests;

internal enum JsonSchemaType
{
    Object,
    Array,
    String,
    Int32,
    Boolean,
    Null,
}

internal sealed record JsonSchemaProperty (
    JsonSchemaNode Schema,
    bool Required = true);

internal sealed record JsonSchemaNode (
    IReadOnlyList<JsonSchemaType> Types,
    IReadOnlyDictionary<string, JsonSchemaProperty>? Properties = null,
    JsonSchemaNode? ItemSchema = null,
    bool AllowAdditionalProperties = true)
{
    public static JsonSchemaNode Value (JsonSchemaType type)
    {
        return new JsonSchemaNode([type]);
    }

    public static JsonSchemaNode Union (params JsonSchemaType[] types)
    {
        if (types is null || types.Length == 0)
        {
            throw new ArgumentException("At least one type is required.", nameof(types));
        }

        return new JsonSchemaNode(types);
    }

    public static JsonSchemaNode Object (
        IReadOnlyDictionary<string, JsonSchemaProperty> properties,
        bool allowAdditionalProperties = true)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return new JsonSchemaNode(
            Types: [JsonSchemaType.Object],
            Properties: properties,
            ItemSchema: null,
            AllowAdditionalProperties: allowAdditionalProperties);
    }

    public static JsonSchemaNode Object (
        Action<JsonObjectSchemaBuilder> configure,
        bool allowAdditionalProperties = true)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new JsonObjectSchemaBuilder();
        configure(builder);
        return Object(builder.Build(), allowAdditionalProperties);
    }

    public static JsonSchemaNode Array (JsonSchemaNode itemSchema)
    {
        ArgumentNullException.ThrowIfNull(itemSchema);
        return new JsonSchemaNode(
            Types: [JsonSchemaType.Array],
            Properties: null,
            ItemSchema: itemSchema,
            AllowAdditionalProperties: true);
    }

    public static JsonSchemaProperty Required (JsonSchemaNode schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new JsonSchemaProperty(schema, Required: true);
    }

    public static JsonSchemaProperty Optional (JsonSchemaNode schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new JsonSchemaProperty(schema, Required: false);
    }
}

internal sealed class JsonObjectSchemaBuilder
{
    private readonly Dictionary<string, JsonSchemaProperty> properties = new(StringComparer.Ordinal);

    public JsonObjectSchemaBuilder Required (string propertyName, JsonSchemaNode schema)
    {
        AddProperty(propertyName, JsonSchemaNode.Required(schema));
        return this;
    }

    public JsonObjectSchemaBuilder Optional (string propertyName, JsonSchemaNode schema)
    {
        AddProperty(propertyName, JsonSchemaNode.Optional(schema));
        return this;
    }

    public JsonObjectSchemaBuilder RequiredObject (
        string propertyName,
        Action<JsonObjectSchemaBuilder> configure,
        bool allowAdditionalProperties = true)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Required(
            propertyName,
            JsonSchemaNode.Object(configure, allowAdditionalProperties));
    }

    public JsonObjectSchemaBuilder OptionalObject (
        string propertyName,
        Action<JsonObjectSchemaBuilder> configure,
        bool allowAdditionalProperties = true)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Optional(
            propertyName,
            JsonSchemaNode.Object(configure, allowAdditionalProperties));
    }

    public JsonObjectSchemaBuilder RequiredArray (string propertyName, JsonSchemaNode itemSchema)
    {
        return Required(propertyName, JsonSchemaNode.Array(itemSchema));
    }

    public JsonObjectSchemaBuilder OptionalArray (string propertyName, JsonSchemaNode itemSchema)
    {
        return Optional(propertyName, JsonSchemaNode.Array(itemSchema));
    }

    public JsonObjectSchemaBuilder RequiredArrayOfObject (
        string propertyName,
        Action<JsonObjectSchemaBuilder> configure,
        bool allowAdditionalProperties = true)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Required(
            propertyName,
            JsonSchemaNode.Array(JsonSchemaNode.Object(configure, allowAdditionalProperties)));
    }

    public JsonObjectSchemaBuilder OptionalArrayOfObject (
        string propertyName,
        Action<JsonObjectSchemaBuilder> configure,
        bool allowAdditionalProperties = true)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Optional(
            propertyName,
            JsonSchemaNode.Array(JsonSchemaNode.Object(configure, allowAdditionalProperties)));
    }

    internal IReadOnlyDictionary<string, JsonSchemaProperty> Build ()
    {
        return properties;
    }

    private void AddProperty (string propertyName, JsonSchemaProperty property)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name must not be null or whitespace.", nameof(propertyName));
        }

        ArgumentNullException.ThrowIfNull(property.Schema);
        if (!properties.TryAdd(propertyName, property))
        {
            throw new ArgumentException(
                $"Property '{propertyName}' is already defined in this schema.",
                nameof(propertyName));
        }
    }
}
