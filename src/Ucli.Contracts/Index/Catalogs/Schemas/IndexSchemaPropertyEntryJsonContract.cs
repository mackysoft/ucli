using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one schema property entry contract in <c>schemas.catalog.json</c>. </summary>
[Description("Serialized object schema property entry.")]
internal sealed record IndexSchemaPropertyEntryJsonContract
{
    [JsonConstructor]
    public IndexSchemaPropertyEntryJsonContract (
        string? Path,
        string? PropertyType,
        string? DeclaredTypeId,
        bool IsArray,
        string? ElementTypeId,
        bool IsReadOnly)
    {
        this.Path = Path;
        this.PropertyType = PropertyType;
        this.DeclaredTypeId = DeclaredTypeId;
        this.IsArray = IsArray;
        this.ElementTypeId = ElementTypeId;
        this.IsReadOnly = IsReadOnly;
    }

    /// <summary> Gets the normalized <c>SerializedProperty.propertyPath</c> value. </summary>
    [JsonRequired]
    [Description("SerializedProperty path.")]
    public string? Path { get; init; }

    /// <summary> Gets the property-type literal value. </summary>
    [JsonRequired]
    [Description("SerializedProperty type literal.")]
    public string? PropertyType { get; init; }

    /// <summary> Gets the declared-type identifier value. </summary>
    [Description("Declared managed type identifier when available.")]
    public string? DeclaredTypeId { get; init; }

    /// <summary> Gets a value indicating whether property value is an array-like collection. </summary>
    [JsonRequired]
    [Description("Whether the property is array-like.")]
    public bool IsArray { get; init; }

    /// <summary> Gets the element-type identifier for array-like values. </summary>
    [Description("Array element type identifier when available.")]
    public string? ElementTypeId { get; init; }

    /// <summary> Gets a value indicating whether set-style operations must treat this property as read-only. </summary>
    [JsonRequired]
    [Description("Whether set-style operations must treat this property as read-only.")]
    public bool IsReadOnly { get; init; }
}
