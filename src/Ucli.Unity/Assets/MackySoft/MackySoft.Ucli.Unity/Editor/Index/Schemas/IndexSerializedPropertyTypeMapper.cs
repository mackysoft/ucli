using System;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
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
                    return TextVocabulary.GetText(IndexPropertyType.Generic);

                case SerializedPropertyType.Integer:
                    return TextVocabulary.GetText(IndexPropertyType.Integer);

                case SerializedPropertyType.Boolean:
                    return TextVocabulary.GetText(IndexPropertyType.Boolean);

                case SerializedPropertyType.Float:
                    return TextVocabulary.GetText(IndexPropertyType.Float);

                case SerializedPropertyType.String:
                    return TextVocabulary.GetText(IndexPropertyType.String);

                case SerializedPropertyType.Color:
                    return TextVocabulary.GetText(IndexPropertyType.Color);

                case SerializedPropertyType.ObjectReference:
                    return TextVocabulary.GetText(IndexPropertyType.ObjectReference);

                case SerializedPropertyType.LayerMask:
                    return TextVocabulary.GetText(IndexPropertyType.LayerMask);

                case SerializedPropertyType.Enum:
                    return TextVocabulary.GetText(IndexPropertyType.Enum);

                case SerializedPropertyType.Vector2:
                    return TextVocabulary.GetText(IndexPropertyType.Vector2);

                case SerializedPropertyType.Vector3:
                    return TextVocabulary.GetText(IndexPropertyType.Vector3);

                case SerializedPropertyType.Vector4:
                    return TextVocabulary.GetText(IndexPropertyType.Vector4);

                case SerializedPropertyType.Rect:
                    return TextVocabulary.GetText(IndexPropertyType.Rect);

                case SerializedPropertyType.ArraySize:
                    return TextVocabulary.GetText(IndexPropertyType.ArraySize);

                case SerializedPropertyType.Character:
                    return TextVocabulary.GetText(IndexPropertyType.Character);

                case SerializedPropertyType.AnimationCurve:
                    return TextVocabulary.GetText(IndexPropertyType.AnimationCurve);

                case SerializedPropertyType.Bounds:
                    return TextVocabulary.GetText(IndexPropertyType.Bounds);

                case SerializedPropertyType.Gradient:
                    return TextVocabulary.GetText(IndexPropertyType.Gradient);

                case SerializedPropertyType.Quaternion:
                    return TextVocabulary.GetText(IndexPropertyType.Quaternion);

                case SerializedPropertyType.ExposedReference:
                    return TextVocabulary.GetText(IndexPropertyType.ExposedReference);

                case SerializedPropertyType.FixedBufferSize:
                    return TextVocabulary.GetText(IndexPropertyType.FixedBufferSize);

                case SerializedPropertyType.Vector2Int:
                    return TextVocabulary.GetText(IndexPropertyType.Vector2Int);

                case SerializedPropertyType.Vector3Int:
                    return TextVocabulary.GetText(IndexPropertyType.Vector3Int);

                case SerializedPropertyType.RectInt:
                    return TextVocabulary.GetText(IndexPropertyType.RectInt);

                case SerializedPropertyType.BoundsInt:
                    return TextVocabulary.GetText(IndexPropertyType.BoundsInt);

                case SerializedPropertyType.ManagedReference:
                    return TextVocabulary.GetText(IndexPropertyType.ManagedReference);

                case SerializedPropertyType.Hash128:
                    return TextVocabulary.GetText(IndexPropertyType.Hash128);

#if UNITY_6000_0_OR_NEWER
                // NOTE: Enable RenderingLayerMask mapping only on Unity 6000.0 or newer.
                case SerializedPropertyType.RenderingLayerMask:
                    return TextVocabulary.GetText(IndexPropertyType.LayerMask);
#endif

                default:
                    throw new InvalidOperationException($"Unsupported SerializedPropertyType: {propertyType}.");
            }
        }
    }
}