namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one schema property entry contract in <c>schemas.catalog.json</c>. </summary>
/// <param name="Path"> The normalized <c>SerializedProperty.propertyPath</c> value. </param>
/// <param name="PropertyType"> The property-type literal value. </param>
/// <param name="DeclaredTypeId"> The declared-type identifier value. </param>
/// <param name="IsArray"> Whether property value is an array-like collection. </param>
/// <param name="ElementTypeId"> The element-type identifier for array-like values. </param>
/// <param name="IsReadOnly"> Whether set-style operations must treat this property as read-only. </param>
internal sealed record IndexSchemaPropertyEntryJsonContract (
    string? Path,
    string? PropertyType,
    string? DeclaredTypeId,
    bool IsArray,
    string? ElementTypeId,
    bool IsReadOnly);