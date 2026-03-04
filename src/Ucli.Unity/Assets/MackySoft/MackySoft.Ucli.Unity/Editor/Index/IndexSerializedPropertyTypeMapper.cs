using System;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Maps Unity SerializedPropertyType values to index contract property-type literals. </summary>
    internal static class IndexSerializedPropertyTypeMapper
    {
        /// <summary> Converts one SerializedPropertyType to index property-type literal. </summary>
        /// <param name="propertyType"> The SerializedPropertyType value. </param>
        /// <returns> The index property-type literal. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when <paramref name="propertyType" /> is unsupported. </exception>
        public static string ToLiteral (SerializedPropertyType propertyType)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Generic:
                    return IndexPropertyTypeValues.Generic;

                case SerializedPropertyType.Integer:
                    return IndexPropertyTypeValues.Integer;

                case SerializedPropertyType.Boolean:
                    return IndexPropertyTypeValues.Boolean;

                case SerializedPropertyType.Float:
                    return IndexPropertyTypeValues.Float;

                case SerializedPropertyType.String:
                    return IndexPropertyTypeValues.String;

                case SerializedPropertyType.Color:
                    return IndexPropertyTypeValues.Color;

                case SerializedPropertyType.ObjectReference:
                    return IndexPropertyTypeValues.ObjectReference;

                case SerializedPropertyType.LayerMask:
                    return IndexPropertyTypeValues.LayerMask;

                case SerializedPropertyType.Enum:
                    return IndexPropertyTypeValues.Enum;

                case SerializedPropertyType.Vector2:
                    return IndexPropertyTypeValues.Vector2;

                case SerializedPropertyType.Vector3:
                    return IndexPropertyTypeValues.Vector3;

                case SerializedPropertyType.Vector4:
                    return IndexPropertyTypeValues.Vector4;

                case SerializedPropertyType.Rect:
                    return IndexPropertyTypeValues.Rect;

                case SerializedPropertyType.ArraySize:
                    return IndexPropertyTypeValues.ArraySize;

                case SerializedPropertyType.Character:
                    return IndexPropertyTypeValues.Character;

                case SerializedPropertyType.AnimationCurve:
                    return IndexPropertyTypeValues.AnimationCurve;

                case SerializedPropertyType.Bounds:
                    return IndexPropertyTypeValues.Bounds;

                case SerializedPropertyType.Gradient:
                    return IndexPropertyTypeValues.Gradient;

                case SerializedPropertyType.Quaternion:
                    return IndexPropertyTypeValues.Quaternion;

                case SerializedPropertyType.ExposedReference:
                    return IndexPropertyTypeValues.ExposedReference;

                case SerializedPropertyType.FixedBufferSize:
                    return IndexPropertyTypeValues.FixedBufferSize;

                case SerializedPropertyType.Vector2Int:
                    return IndexPropertyTypeValues.Vector2Int;

                case SerializedPropertyType.Vector3Int:
                    return IndexPropertyTypeValues.Vector3Int;

                case SerializedPropertyType.RectInt:
                    return IndexPropertyTypeValues.RectInt;

                case SerializedPropertyType.BoundsInt:
                    return IndexPropertyTypeValues.BoundsInt;

                case SerializedPropertyType.ManagedReference:
                    return IndexPropertyTypeValues.ManagedReference;

                case SerializedPropertyType.Hash128:
                    return IndexPropertyTypeValues.Hash128;

                default:
                    throw new InvalidOperationException($"Unsupported SerializedPropertyType: {propertyType}.");
            }
        }
    }
}
