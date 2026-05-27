using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines stable property-type literals stored in index schema contracts. </summary>
public enum IndexPropertyType
{
    /// <summary> Represents Unity <c>SerializedPropertyType.Generic</c>. </summary>
    [UcliContractLiteral("generic")]
    Generic = 0,

    /// <summary> Represents Unity <c>SerializedPropertyType.Integer</c>. </summary>
    [UcliContractLiteral("integer")]
    Integer = 1,

    /// <summary> Represents Unity <c>SerializedPropertyType.Boolean</c>. </summary>
    [UcliContractLiteral("boolean")]
    Boolean = 2,

    /// <summary> Represents Unity <c>SerializedPropertyType.Float</c>. </summary>
    [UcliContractLiteral("float")]
    Float = 3,

    /// <summary> Represents Unity <c>SerializedPropertyType.String</c>. </summary>
    [UcliContractLiteral("string")]
    String = 4,

    /// <summary> Represents Unity <c>SerializedPropertyType.Color</c>. </summary>
    [UcliContractLiteral("color")]
    Color = 5,

    /// <summary> Represents Unity <c>SerializedPropertyType.ObjectReference</c>. </summary>
    [UcliContractLiteral("objectReference")]
    ObjectReference = 6,

    /// <summary> Represents Unity <c>SerializedPropertyType.LayerMask</c>. </summary>
    [UcliContractLiteral("layerMask")]
    LayerMask = 7,

    /// <summary> Represents Unity <c>SerializedPropertyType.Enum</c>. </summary>
    [UcliContractLiteral("enum")]
    Enum = 8,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector2</c>. </summary>
    [UcliContractLiteral("vector2")]
    Vector2 = 9,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector3</c>. </summary>
    [UcliContractLiteral("vector3")]
    Vector3 = 10,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector4</c>. </summary>
    [UcliContractLiteral("vector4")]
    Vector4 = 11,

    /// <summary> Represents Unity <c>SerializedPropertyType.Rect</c>. </summary>
    [UcliContractLiteral("rect")]
    Rect = 12,

    /// <summary> Represents Unity <c>SerializedPropertyType.ArraySize</c>. </summary>
    [UcliContractLiteral("arraySize")]
    ArraySize = 13,

    /// <summary> Represents Unity <c>SerializedPropertyType.Character</c>. </summary>
    [UcliContractLiteral("character")]
    Character = 14,

    /// <summary> Represents Unity <c>SerializedPropertyType.AnimationCurve</c>. </summary>
    [UcliContractLiteral("animationCurve")]
    AnimationCurve = 15,

    /// <summary> Represents Unity <c>SerializedPropertyType.Bounds</c>. </summary>
    [UcliContractLiteral("bounds")]
    Bounds = 16,

    /// <summary> Represents Unity <c>SerializedPropertyType.Gradient</c>. </summary>
    [UcliContractLiteral("gradient")]
    Gradient = 17,

    /// <summary> Represents Unity <c>SerializedPropertyType.Quaternion</c>. </summary>
    [UcliContractLiteral("quaternion")]
    Quaternion = 18,

    /// <summary> Represents Unity <c>SerializedPropertyType.ExposedReference</c>. </summary>
    [UcliContractLiteral("exposedReference")]
    ExposedReference = 19,

    /// <summary> Represents Unity <c>SerializedPropertyType.FixedBufferSize</c>. </summary>
    [UcliContractLiteral("fixedBufferSize")]
    FixedBufferSize = 20,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector2Int</c>. </summary>
    [UcliContractLiteral("vector2Int")]
    Vector2Int = 21,

    /// <summary> Represents Unity <c>SerializedPropertyType.Vector3Int</c>. </summary>
    [UcliContractLiteral("vector3Int")]
    Vector3Int = 22,

    /// <summary> Represents Unity <c>SerializedPropertyType.RectInt</c>. </summary>
    [UcliContractLiteral("rectInt")]
    RectInt = 23,

    /// <summary> Represents Unity <c>SerializedPropertyType.BoundsInt</c>. </summary>
    [UcliContractLiteral("boundsInt")]
    BoundsInt = 24,

    /// <summary> Represents Unity <c>SerializedPropertyType.ManagedReference</c>. </summary>
    [UcliContractLiteral("managedReference")]
    ManagedReference = 25,

    /// <summary> Represents Unity <c>SerializedPropertyType.Hash128</c>. </summary>
    [UcliContractLiteral("hash128")]
    Hash128 = 26,
}
