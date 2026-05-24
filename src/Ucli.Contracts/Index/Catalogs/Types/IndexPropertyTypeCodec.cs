using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Converts index property-type values between enum and contract literals. </summary>
public static class IndexPropertyTypeCodec
{
    private static readonly (IndexPropertyType Value, string Literal)[] Mappings =
    {
        (IndexPropertyType.Generic, IndexPropertyTypeValues.Generic),
        (IndexPropertyType.Integer, IndexPropertyTypeValues.Integer),
        (IndexPropertyType.Boolean, IndexPropertyTypeValues.Boolean),
        (IndexPropertyType.Float, IndexPropertyTypeValues.Float),
        (IndexPropertyType.String, IndexPropertyTypeValues.String),
        (IndexPropertyType.Color, IndexPropertyTypeValues.Color),
        (IndexPropertyType.ObjectReference, IndexPropertyTypeValues.ObjectReference),
        (IndexPropertyType.LayerMask, IndexPropertyTypeValues.LayerMask),
        (IndexPropertyType.Enum, IndexPropertyTypeValues.Enum),
        (IndexPropertyType.Vector2, IndexPropertyTypeValues.Vector2),
        (IndexPropertyType.Vector3, IndexPropertyTypeValues.Vector3),
        (IndexPropertyType.Vector4, IndexPropertyTypeValues.Vector4),
        (IndexPropertyType.Rect, IndexPropertyTypeValues.Rect),
        (IndexPropertyType.ArraySize, IndexPropertyTypeValues.ArraySize),
        (IndexPropertyType.Character, IndexPropertyTypeValues.Character),
        (IndexPropertyType.AnimationCurve, IndexPropertyTypeValues.AnimationCurve),
        (IndexPropertyType.Bounds, IndexPropertyTypeValues.Bounds),
        (IndexPropertyType.Gradient, IndexPropertyTypeValues.Gradient),
        (IndexPropertyType.Quaternion, IndexPropertyTypeValues.Quaternion),
        (IndexPropertyType.ExposedReference, IndexPropertyTypeValues.ExposedReference),
        (IndexPropertyType.FixedBufferSize, IndexPropertyTypeValues.FixedBufferSize),
        (IndexPropertyType.Vector2Int, IndexPropertyTypeValues.Vector2Int),
        (IndexPropertyType.Vector3Int, IndexPropertyTypeValues.Vector3Int),
        (IndexPropertyType.RectInt, IndexPropertyTypeValues.RectInt),
        (IndexPropertyType.BoundsInt, IndexPropertyTypeValues.BoundsInt),
        (IndexPropertyType.ManagedReference, IndexPropertyTypeValues.ManagedReference),
        (IndexPropertyType.Hash128, IndexPropertyTypeValues.Hash128),
    };

    /// <summary> Converts one index property-type enum value to schema literal. </summary>
    /// <param name="propertyType"> The property-type enum value. </param>
    /// <returns> The schema literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="propertyType" /> is unsupported. </exception>
    public static string ToValue (IndexPropertyType propertyType)
    {
        return LiteralCodecUtilities.ToValue(
            propertyType,
            Mappings,
            nameof(propertyType),
            "Unsupported index propertyType.");
    }

    /// <summary> Tries to parse one schema literal to index property-type enum. </summary>
    /// <param name="value"> The schema literal value. </param>
    /// <param name="propertyType"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IndexPropertyType propertyType)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.OrdinalIgnoreCase,
            out propertyType);
    }
}
