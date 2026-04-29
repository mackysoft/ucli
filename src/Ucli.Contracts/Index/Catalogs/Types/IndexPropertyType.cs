namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines stable property-type literals stored in index schema contracts. </summary>
public enum IndexPropertyType
{
    /// <summary> Represents Unity <c>SerializedPropertyType.Generic</c>. </summary>
    Generic = 0,

    /// <summary> Represents Unity <c>SerializedPropertyType.Integer</c>. </summary>
    Integer = 1,

    /// <summary> Represents Unity <c>SerializedPropertyType.Boolean</c>. </summary>
    Boolean = 2,

    /// <summary> Represents Unity <c>SerializedPropertyType.Float</c>. </summary>
    Float = 3,

    /// <summary> Represents Unity <c>SerializedPropertyType.String</c>. </summary>
    String = 4,

    /// <summary> Represents Unity <c>SerializedPropertyType.Color</c>. </summary>
    Color = 5,

    /// <summary> Represents Unity <c>SerializedPropertyType.ObjectReference</c>. </summary>
    ObjectReference = 6,

    /// <summary> Represents Unity <c>SerializedPropertyType.LayerMask</c>. </summary>
    LayerMask = 7,

    /// <summary> Represents Unity <c>SerializedPropertyType.Enum</c>. </summary>
    Enum = 8,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector2</c>. </summary>
    Vector2 = 9,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector3</c>. </summary>
    Vector3 = 10,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector4</c>. </summary>
    Vector4 = 11,

    /// <summary> Represents Unity <c>SerializedPropertyType.Rect</c>. </summary>
    Rect = 12,

    /// <summary> Represents Unity <c>SerializedPropertyType.ArraySize</c>. </summary>
    ArraySize = 13,

    /// <summary> Represents Unity <c>SerializedPropertyType.Character</c>. </summary>
    Character = 14,

    /// <summary> Represents Unity <c>SerializedPropertyType.AnimationCurve</c>. </summary>
    AnimationCurve = 15,

    /// <summary> Represents Unity <c>SerializedPropertyType.Bounds</c>. </summary>
    Bounds = 16,

    /// <summary> Represents Unity <c>SerializedPropertyType.Gradient</c>. </summary>
    Gradient = 17,

    /// <summary> Represents Unity <c>SerializedPropertyType.Quaternion</c>. </summary>
    Quaternion = 18,

    /// <summary> Represents Unity <c>SerializedPropertyType.ExposedReference</c>. </summary>
    ExposedReference = 19,

    /// <summary> Represents Unity <c>SerializedPropertyType.FixedBufferSize</c>. </summary>
    FixedBufferSize = 20,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector2Int</c>. </summary>
    Vector2Int = 21,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector3Int</c>. </summary>
    Vector3Int = 22,

    /// <summary> Represents Unity <c>SerializedPropertyType.RectInt</c>. </summary>
    RectInt = 23,

    /// <summary> Represents Unity <c>SerializedPropertyType.BoundsInt</c>. </summary>
    BoundsInt = 24,

    /// <summary> Represents Unity <c>SerializedPropertyType.ManagedReference</c>. </summary>
    ManagedReference = 25,

    /// <summary> Represents Unity <c>SerializedPropertyType.Hash128</c>. </summary>
    Hash128 = 26,
}
