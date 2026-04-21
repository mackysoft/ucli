namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Converts index property-type values between enum and contract literals. </summary>
public static class IndexPropertyTypeCodec
{
    /// <summary> Converts one index property-type enum value to schema literal. </summary>
    /// <param name="propertyType"> The property-type enum value. </param>
    /// <returns> The schema literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="propertyType" /> is unsupported. </exception>
    public static string ToValue (IndexPropertyType propertyType)
    {
        return propertyType switch
        {
            IndexPropertyType.Generic => IndexPropertyTypeValues.Generic,
            IndexPropertyType.Integer => IndexPropertyTypeValues.Integer,
            IndexPropertyType.Boolean => IndexPropertyTypeValues.Boolean,
            IndexPropertyType.Float => IndexPropertyTypeValues.Float,
            IndexPropertyType.String => IndexPropertyTypeValues.String,
            IndexPropertyType.Color => IndexPropertyTypeValues.Color,
            IndexPropertyType.ObjectReference => IndexPropertyTypeValues.ObjectReference,
            IndexPropertyType.LayerMask => IndexPropertyTypeValues.LayerMask,
            IndexPropertyType.Enum => IndexPropertyTypeValues.Enum,
            IndexPropertyType.Vector2 => IndexPropertyTypeValues.Vector2,
            IndexPropertyType.Vector3 => IndexPropertyTypeValues.Vector3,
            IndexPropertyType.Vector4 => IndexPropertyTypeValues.Vector4,
            IndexPropertyType.Rect => IndexPropertyTypeValues.Rect,
            IndexPropertyType.ArraySize => IndexPropertyTypeValues.ArraySize,
            IndexPropertyType.Character => IndexPropertyTypeValues.Character,
            IndexPropertyType.AnimationCurve => IndexPropertyTypeValues.AnimationCurve,
            IndexPropertyType.Bounds => IndexPropertyTypeValues.Bounds,
            IndexPropertyType.Gradient => IndexPropertyTypeValues.Gradient,
            IndexPropertyType.Quaternion => IndexPropertyTypeValues.Quaternion,
            IndexPropertyType.ExposedReference => IndexPropertyTypeValues.ExposedReference,
            IndexPropertyType.FixedBufferSize => IndexPropertyTypeValues.FixedBufferSize,
            IndexPropertyType.Vector2Int => IndexPropertyTypeValues.Vector2Int,
            IndexPropertyType.Vector3Int => IndexPropertyTypeValues.Vector3Int,
            IndexPropertyType.RectInt => IndexPropertyTypeValues.RectInt,
            IndexPropertyType.BoundsInt => IndexPropertyTypeValues.BoundsInt,
            IndexPropertyType.ManagedReference => IndexPropertyTypeValues.ManagedReference,
            IndexPropertyType.Hash128 => IndexPropertyTypeValues.Hash128,
            _ => throw new ArgumentOutOfRangeException(nameof(propertyType), propertyType, "Unsupported index propertyType."),
        };
    }

    /// <summary> Tries to parse one schema literal to index property-type enum. </summary>
    /// <param name="value"> The schema literal value. </param>
    /// <param name="propertyType"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IndexPropertyType propertyType)
    {
        if (string.Equals(value, IndexPropertyTypeValues.Generic, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Generic;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Integer, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Integer;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Boolean, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Boolean;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Float, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Float;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.String, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.String;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Color, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Color;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.ObjectReference, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.ObjectReference;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.LayerMask, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.LayerMask;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Enum, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Enum;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Vector2, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Vector2;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Vector3, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Vector3;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Vector4, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Vector4;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Rect, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Rect;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.ArraySize, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.ArraySize;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Character, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Character;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.AnimationCurve, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.AnimationCurve;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Bounds, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Bounds;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Gradient, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Gradient;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Quaternion, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Quaternion;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.ExposedReference, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.ExposedReference;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.FixedBufferSize, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.FixedBufferSize;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Vector2Int, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Vector2Int;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Vector3Int, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Vector3Int;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.RectInt, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.RectInt;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.BoundsInt, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.BoundsInt;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.ManagedReference, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.ManagedReference;
            return true;
        }

        if (string.Equals(value, IndexPropertyTypeValues.Hash128, StringComparison.OrdinalIgnoreCase))
        {
            propertyType = IndexPropertyType.Hash128;
            return true;
        }

        propertyType = default;
        return false;
    }
}