using System;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;

#nullable enable

using MackySoft.Ucli.Contracts.Text;

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
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Generic);

                case SerializedPropertyType.Integer:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Integer);

                case SerializedPropertyType.Boolean:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Boolean);

                case SerializedPropertyType.Float:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Float);

                case SerializedPropertyType.String:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.String);

                case SerializedPropertyType.Color:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Color);

                case SerializedPropertyType.ObjectReference:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.ObjectReference);

                case SerializedPropertyType.LayerMask:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.LayerMask);

                case SerializedPropertyType.Enum:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Enum);

                case SerializedPropertyType.Vector2:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Vector2);

                case SerializedPropertyType.Vector3:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Vector3);

                case SerializedPropertyType.Vector4:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Vector4);

                case SerializedPropertyType.Rect:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Rect);

                case SerializedPropertyType.ArraySize:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.ArraySize);

                case SerializedPropertyType.Character:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Character);

                case SerializedPropertyType.AnimationCurve:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.AnimationCurve);

                case SerializedPropertyType.Bounds:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Bounds);

                case SerializedPropertyType.Gradient:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Gradient);

                case SerializedPropertyType.Quaternion:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Quaternion);

                case SerializedPropertyType.ExposedReference:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.ExposedReference);

                case SerializedPropertyType.FixedBufferSize:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.FixedBufferSize);

                case SerializedPropertyType.Vector2Int:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Vector2Int);

                case SerializedPropertyType.Vector3Int:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Vector3Int);

                case SerializedPropertyType.RectInt:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.RectInt);

                case SerializedPropertyType.BoundsInt:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.BoundsInt);

                case SerializedPropertyType.ManagedReference:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.ManagedReference);

                case SerializedPropertyType.Hash128:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.Hash128);

#if UNITY_6000_0_OR_NEWER
                // NOTE: Enable RenderingLayerMask mapping only on Unity 6000.0 or newer.
                case SerializedPropertyType.RenderingLayerMask:
                    return ContractLiteralCodec.ToValue(IndexPropertyType.LayerMask);
#endif

                default:
                    throw new InvalidOperationException($"Unsupported SerializedPropertyType: {propertyType}.");
            }
        }
    }
}