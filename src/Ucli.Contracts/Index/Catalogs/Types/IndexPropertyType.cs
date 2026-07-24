
namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines stable property-type literals stored in index schema contracts. </summary>
[VocabularyDefinition]
public enum IndexPropertyType
{
    /// <summary> Represents Unity <c>SerializedPropertyType.Generic</c>. </summary>
    [VocabularyText("generic")]
    Generic = 0,

    /// <summary> Represents Unity <c>SerializedPropertyType.Integer</c>. </summary>
    [VocabularyText("integer")]
    Integer = 1,

    /// <summary> Represents Unity <c>SerializedPropertyType.Boolean</c>. </summary>
    [VocabularyText("boolean")]
    Boolean = 2,

    /// <summary> Represents Unity <c>SerializedPropertyType.Float</c>. </summary>
    [VocabularyText("float")]
    Float = 3,

    /// <summary> Represents Unity <c>SerializedPropertyType.String</c>. </summary>
    [VocabularyText("string")]
    String = 4,

    /// <summary> Represents Unity <c>SerializedPropertyType.Color</c>. </summary>
    [VocabularyText("color")]
    Color = 5,

    /// <summary> Represents Unity <c>SerializedPropertyType.ObjectReference</c>. </summary>
    [VocabularyText("objectReference")]
    ObjectReference = 6,

    /// <summary> Represents Unity <c>SerializedPropertyType.LayerMask</c>. </summary>
    [VocabularyText("layerMask")]
    LayerMask = 7,

    /// <summary> Represents Unity <c>SerializedPropertyType.Enum</c>. </summary>
    [VocabularyText("enum")]
    Enum = 8,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector2</c>. </summary>
    [VocabularyText("vector2")]
    Vector2 = 9,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector3</c>. </summary>
    [VocabularyText("vector3")]
    Vector3 = 10,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector4</c>. </summary>
    [VocabularyText("vector4")]
    Vector4 = 11,

    /// <summary> Represents Unity <c>SerializedPropertyType.Rect</c>. </summary>
    [VocabularyText("rect")]
    Rect = 12,

    /// <summary> Represents Unity <c>SerializedPropertyType.ArraySize</c>. </summary>
    [VocabularyText("arraySize")]
    ArraySize = 13,

    /// <summary> Represents Unity <c>SerializedPropertyType.Character</c>. </summary>
    [VocabularyText("character")]
    Character = 14,

    /// <summary> Represents Unity <c>SerializedPropertyType.AnimationCurve</c>. </summary>
    [VocabularyText("animationCurve")]
    AnimationCurve = 15,

    /// <summary> Represents Unity <c>SerializedPropertyType.Bounds</c>. </summary>
    [VocabularyText("bounds")]
    Bounds = 16,

    /// <summary> Represents Unity <c>SerializedPropertyType.Gradient</c>. </summary>
    [VocabularyText("gradient")]
    Gradient = 17,

    /// <summary> Represents Unity <c>SerializedPropertyType.Quaternion</c>. </summary>
    [VocabularyText("quaternion")]
    Quaternion = 18,

    /// <summary> Represents Unity <c>SerializedPropertyType.ExposedReference</c>. </summary>
    [VocabularyText("exposedReference")]
    ExposedReference = 19,

    /// <summary> Represents Unity <c>SerializedPropertyType.FixedBufferSize</c>. </summary>
    [VocabularyText("fixedBufferSize")]
    FixedBufferSize = 20,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector2Int</c>. </summary>
    [VocabularyText("vector2Int")]
    Vector2Int = 21,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector3Int</c>. </summary>
    [VocabularyText("vector3Int")]
    Vector3Int = 22,

    /// <summary> Represents Unity <c>SerializedPropertyType.RectInt</c>. </summary>
    [VocabularyText("rectInt")]
    RectInt = 23,

    /// <summary> Represents Unity <c>SerializedPropertyType.BoundsInt</c>. </summary>
    [VocabularyText("boundsInt")]
    BoundsInt = 24,

    /// <summary> Represents Unity <c>SerializedPropertyType.ManagedReference</c>. </summary>
    [VocabularyText("managedReference")]
    ManagedReference = 25,

    /// <summary> Represents Unity <c>SerializedPropertyType.Hash128</c>. </summary>
    [VocabularyText("hash128")]
    Hash128 = 26,
}
