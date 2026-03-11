using System;
using MackySoft.Ucli.Unity.Index;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves declared or inferred runtime types for serialized properties. </summary>
    internal static class SerializedObjectDeclaredTypeResolver
    {
        /// <summary> Resolves one serialized-property type from root runtime type and serialized state. </summary>
        /// <param name="serializedObject"> The owning serialized object. </param>
        /// <param name="rootType"> The serialized root runtime type. </param>
        /// <param name="property"> The target serialized property. </param>
        /// <param name="declaredType"> The resolved runtime type when successful. </param>
        /// <param name="errorMessage"> The failure message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the property type is resolved or inferred; otherwise <see langword="false" />. </returns>
        public static bool TryResolve (
            SerializedObject serializedObject,
            Type rootType,
            SerializedProperty property,
            out Type declaredType,
            out string errorMessage)
        {
            if (serializedObject == null)
            {
                throw new ArgumentNullException(nameof(serializedObject));
            }

            if (rootType == null)
            {
                throw new ArgumentNullException(nameof(rootType));
            }

            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            if (property.propertyType == SerializedPropertyType.ArraySize
                || property.propertyType == SerializedPropertyType.FixedBufferSize)
            {
                declaredType = typeof(int);
                errorMessage = string.Empty;
                return true;
            }

            var resolution = IndexDeclaredTypeResolver.Resolve(rootType, property.propertyPath);
            if (resolution.IsResolved)
            {
                declaredType = resolution.DeclaredType;
                errorMessage = string.Empty;
                return true;
            }

            if (TryResolveManagedReferenceDeclaredType(serializedObject, property, out declaredType, out errorMessage))
            {
                return true;
            }

            if (TryInferFromSerializedProperty(property, out declaredType))
            {
                errorMessage = string.Empty;
                return true;
            }

            declaredType = null!;
            errorMessage = $"Declared type could not be resolved for SerializedProperty path '{property.propertyPath}'.";
            return false;
        }

        private static bool TryResolveManagedReferenceDeclaredType (
            SerializedObject serializedObject,
            SerializedProperty property,
            out Type declaredType,
            out string errorMessage)
        {
            var propertyPath = property.propertyPath;
            var originalPropertyPath = propertyPath;
            while (TryGetParentPropertyPath(propertyPath, out var parentPath))
            {
                var parentProperty = serializedObject.FindProperty(parentPath);
                if (parentProperty != null
                    && parentProperty.propertyType == SerializedPropertyType.ManagedReference
                    && parentProperty.managedReferenceValue != null)
                {
                    var relativePath = originalPropertyPath.Substring(parentPath.Length + 1);
                    var resolution = IndexDeclaredTypeResolver.Resolve(parentProperty.managedReferenceValue.GetType(), relativePath);
                    if (resolution.IsResolved)
                    {
                        declaredType = resolution.DeclaredType;
                        errorMessage = string.Empty;
                        return true;
                    }
                }

                propertyPath = parentPath;
            }

            declaredType = null!;
            errorMessage = string.Empty;
            return false;
        }

        private static bool TryInferFromSerializedProperty (
            SerializedProperty property,
            out Type declaredType)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    declaredType = ResolveIntegerNumericType(property);
                    return true;

                case SerializedPropertyType.LayerMask:
                    declaredType = typeof(LayerMask);
                    return true;

                case SerializedPropertyType.Boolean:
                    declaredType = typeof(bool);
                    return true;

                case SerializedPropertyType.Float:
                    declaredType = ResolveFloatNumericType(property);
                    return true;

                case SerializedPropertyType.String:
                    declaredType = typeof(string);
                    return true;

                case SerializedPropertyType.Color:
                    declaredType = typeof(Color);
                    return true;

                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.ExposedReference:
                    declaredType = property.objectReferenceValue != null
                        ? property.objectReferenceValue.GetType()
                        : typeof(UnityEngine.Object);
                    return true;

                case SerializedPropertyType.Enum:
                    if (TryGetBoxedValueType(property, out declaredType) && declaredType.IsEnum)
                    {
                        return true;
                    }

                    declaredType = typeof(int);
                    return true;

                case SerializedPropertyType.Vector2:
                    declaredType = typeof(Vector2);
                    return true;

                case SerializedPropertyType.Vector3:
                    declaredType = typeof(Vector3);
                    return true;

                case SerializedPropertyType.Vector4:
                    declaredType = typeof(Vector4);
                    return true;

                case SerializedPropertyType.Rect:
                    declaredType = typeof(Rect);
                    return true;

                case SerializedPropertyType.Character:
                    declaredType = typeof(char);
                    return true;

                case SerializedPropertyType.AnimationCurve:
                    declaredType = typeof(AnimationCurve);
                    return true;

                case SerializedPropertyType.Bounds:
                    declaredType = typeof(Bounds);
                    return true;

                case SerializedPropertyType.Gradient:
                    declaredType = typeof(Gradient);
                    return true;

                case SerializedPropertyType.Quaternion:
                    declaredType = typeof(Quaternion);
                    return true;

                case SerializedPropertyType.Vector2Int:
                    declaredType = typeof(Vector2Int);
                    return true;

                case SerializedPropertyType.Vector3Int:
                    declaredType = typeof(Vector3Int);
                    return true;

                case SerializedPropertyType.RectInt:
                    declaredType = typeof(RectInt);
                    return true;

                case SerializedPropertyType.BoundsInt:
                    declaredType = typeof(BoundsInt);
                    return true;

                case SerializedPropertyType.ManagedReference:
                    declaredType = property.managedReferenceValue != null
                        ? property.managedReferenceValue.GetType()
                        : typeof(object);
                    return true;

                case SerializedPropertyType.Hash128:
                    declaredType = typeof(Hash128);
                    return true;

                case SerializedPropertyType.Generic:
                    if (TryGetBoxedValueType(property, out declaredType))
                    {
                        return true;
                    }

                    declaredType = typeof(object);
                    return true;

                default:
                    declaredType = null!;
                    return false;
            }
        }

        private static Type ResolveIntegerNumericType (SerializedProperty property)
        {
            switch (property.numericType)
            {
                case SerializedPropertyNumericType.UInt64:
                    return typeof(ulong);

                case SerializedPropertyNumericType.Int64:
                    return typeof(long);

                case SerializedPropertyNumericType.UInt32:
                    return typeof(uint);

                case SerializedPropertyNumericType.UInt16:
                    return typeof(ushort);

                case SerializedPropertyNumericType.UInt8:
                    return typeof(byte);

                case SerializedPropertyNumericType.Int16:
                    return typeof(short);

                case SerializedPropertyNumericType.Int8:
                    return typeof(sbyte);

                case SerializedPropertyNumericType.Int32:
                default:
                    return typeof(int);
            }
        }

        private static Type ResolveFloatNumericType (SerializedProperty property)
        {
            switch (property.numericType)
            {
                case SerializedPropertyNumericType.Double:
                    return typeof(double);

                case SerializedPropertyNumericType.Float:
                default:
                    return typeof(float);
            }
        }

        private static bool TryGetBoxedValueType (
            SerializedProperty property,
            out Type declaredType)
        {
            try
            {
                var boxedValue = property.boxedValue;
                if (boxedValue != null)
                {
                    declaredType = boxedValue.GetType();
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (NullReferenceException)
            {
            }

            declaredType = null!;
            return false;
        }

        private static bool TryGetParentPropertyPath (
            string propertyPath,
            out string parentPath)
        {
            var parentSeparatorIndex = propertyPath.LastIndexOf('.');
            if (parentSeparatorIndex < 0)
            {
                parentPath = string.Empty;
                return false;
            }

            parentPath = propertyPath.Substring(0, parentSeparatorIndex);
            return true;
        }
    }
}