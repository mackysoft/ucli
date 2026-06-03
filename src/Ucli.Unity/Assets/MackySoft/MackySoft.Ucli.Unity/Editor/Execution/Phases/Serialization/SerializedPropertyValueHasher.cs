using System.Globalization;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Creates stable comparison hashes for serialized property values in one request context. </summary>
    internal static class SerializedPropertyValueHasher
    {
        /// <summary> Creates a stable comparison hash for one serialized property value. </summary>
        /// <param name="property"> The serialized property to hash. </param>
        /// <param name="executionContext"> The request execution context used to normalize request-local preview object references. </param>
        /// <param name="resource"> The logical resource that owns <paramref name="property" />. </param>
        /// <returns> A stable string that changes when the serialized property value changes. </returns>
        public static string Create (
            SerializedProperty property,
            OperationExecutionContext executionContext,
            OperationResource resource)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                return $"array:{property.arraySize}:{property.contentHash}";
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                    return $"long:{property.longValue}";

                case SerializedPropertyType.Boolean:
                    return $"bool:{property.boolValue}";

                case SerializedPropertyType.Float:
                    return $"double:{FormatDouble(property.doubleValue)}";

                case SerializedPropertyType.String:
                    return $"string:{property.stringValue}";

                case SerializedPropertyType.Color:
                    return $"color:{FormatColor(property.colorValue)}";

                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.ExposedReference:
                    return CreateObjectReferenceValueHash(property, executionContext, resource);

                case SerializedPropertyType.Enum:
                    return $"enum:{property.enumValueIndex}";

                case SerializedPropertyType.Vector2:
                    return $"vector2:{FormatVector2(property.vector2Value)}";

                case SerializedPropertyType.Vector3:
                    return $"vector3:{FormatVector3(property.vector3Value)}";

                case SerializedPropertyType.Vector4:
                    return $"vector4:{FormatVector4(property.vector4Value)}";

                case SerializedPropertyType.Rect:
                    return $"rect:{FormatRect(property.rectValue)}";

                case SerializedPropertyType.Bounds:
                    return $"bounds:{FormatBounds(property.boundsValue)}";

                case SerializedPropertyType.Quaternion:
                    return $"quaternion:{FormatQuaternion(property.quaternionValue)}";

                case SerializedPropertyType.Vector2Int:
                    return $"vector2int:{FormatVector2Int(property.vector2IntValue)}";

                case SerializedPropertyType.Vector3Int:
                    return $"vector3int:{FormatVector3Int(property.vector3IntValue)}";

                case SerializedPropertyType.RectInt:
                    return $"rectint:{FormatRectInt(property.rectIntValue)}";

                case SerializedPropertyType.BoundsInt:
                    return $"boundsint:{FormatBoundsInt(property.boundsIntValue)}";

                case SerializedPropertyType.Hash128:
                    return $"hash128:{property.hash128Value}";

                default:
                    return $"hash:{property.contentHash}";
            }
        }

        private static string CreateObjectReferenceValueHash (
            SerializedProperty property,
            OperationExecutionContext executionContext,
            OperationResource resource)
        {
            var unityObject = property.propertyType == SerializedPropertyType.ExposedReference
                ? property.exposedReferenceValue
                : property.objectReferenceValue;
            if (unityObject == null)
            {
                return "object:null";
            }

            if (executionContext.TryResolvePreviewSourceTrackingKey(unityObject, resource, out var previewSourceTrackingKey))
            {
                return $"object:{previewSourceTrackingKey}";
            }

            return $"object:{UnityObjectReferenceResolver.CreateTrackingKey(unityObject)}";
        }

        private static string FormatSingle (float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string FormatDouble (double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string FormatColor (Color value)
        {
            return $"{FormatSingle(value.r)},{FormatSingle(value.g)},{FormatSingle(value.b)},{FormatSingle(value.a)}";
        }

        private static string FormatVector2 (Vector2 value)
        {
            return $"{FormatSingle(value.x)},{FormatSingle(value.y)}";
        }

        private static string FormatVector3 (Vector3 value)
        {
            return $"{FormatSingle(value.x)},{FormatSingle(value.y)},{FormatSingle(value.z)}";
        }

        private static string FormatVector4 (Vector4 value)
        {
            return $"{FormatSingle(value.x)},{FormatSingle(value.y)},{FormatSingle(value.z)},{FormatSingle(value.w)}";
        }

        private static string FormatRect (Rect value)
        {
            return $"{FormatSingle(value.x)},{FormatSingle(value.y)},{FormatSingle(value.width)},{FormatSingle(value.height)}";
        }

        private static string FormatBounds (Bounds value)
        {
            return $"{FormatVector3(value.center)}|{FormatVector3(value.size)}";
        }

        private static string FormatQuaternion (Quaternion value)
        {
            return $"{FormatSingle(value.x)},{FormatSingle(value.y)},{FormatSingle(value.z)},{FormatSingle(value.w)}";
        }

        private static string FormatVector2Int (Vector2Int value)
        {
            return $"{value.x},{value.y}";
        }

        private static string FormatVector3Int (Vector3Int value)
        {
            return $"{value.x},{value.y},{value.z}";
        }

        private static string FormatRectInt (RectInt value)
        {
            return $"{value.x},{value.y},{value.width},{value.height}";
        }

        private static string FormatBoundsInt (BoundsInt value)
        {
            return $"{FormatVector3Int(value.position)}|{FormatVector3Int(value.size)}";
        }
    }
}
