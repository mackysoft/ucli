using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Resolves declared field/runtime types from one SerializedProperty path. </summary>
    internal static class IndexDeclaredTypeResolver
    {
        /// <summary> Resolves one declared runtime type from root type and SerializedProperty path. </summary>
        /// <param name="rootType"> The root serialized type. </param>
        /// <param name="propertyPath"> The SerializedProperty path. </param>
        /// <returns> The declared type resolution result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="rootType" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="propertyPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        public static IndexDeclaredTypeResolution Resolve (
            Type rootType,
            string propertyPath)
        {
            if (rootType == null)
            {
                throw new ArgumentNullException(nameof(rootType));
            }

            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                throw new ArgumentException("Property path must not be empty.", nameof(propertyPath));
            }

            var currentType = rootType;
            Type? currentElementType = null;
            var segments = propertyPath.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == "Array")
                {
                    continue;
                }

                if (segment.StartsWith("data[", StringComparison.Ordinal))
                {
                    var resolvedElementType = TryResolveElementType(currentType);
                    if (resolvedElementType == null)
                    {
                        return new IndexDeclaredTypeResolution(currentType, currentElementType, false);
                    }

                    currentType = resolvedElementType;
                    currentElementType = resolvedElementType;
                    continue;
                }

                var fieldInfo = ResolveField(currentType, segment);
                if (fieldInfo == null)
                {
                    if (TryResolveUnityNativeDeclaredType(currentType, segment, out var unityNativeType))
                    {
                        currentType = unityNativeType;
                        currentElementType = TryResolveElementType(currentType);
                        continue;
                    }

                    return new IndexDeclaredTypeResolution(currentType, currentElementType, false);
                }

                currentType = fieldInfo.FieldType;
                currentElementType = TryResolveElementType(currentType);
            }

            return new IndexDeclaredTypeResolution(currentType, currentElementType, true);
        }

        /// <summary> Tries to resolve one collection element type from one runtime type. </summary>
        /// <param name="type"> The runtime type. </param>
        /// <returns> The collection element type when available; otherwise <see langword="null" />. </returns>
        public static Type? TryResolveElementType (Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (!type.IsGenericType)
            {
                return null;
            }

            var genericArguments = type.GetGenericArguments();
            if (genericArguments.Length == 1)
            {
                return genericArguments[0];
            }

            return null;
        }

        private static FieldInfo? ResolveField (
            Type type,
            string fieldName)
        {
            var searchType = type;
            while (searchType != null)
            {
                var fieldInfo = searchType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    return fieldInfo;
                }

                searchType = searchType.BaseType;
            }

            return null;
        }

        private static bool TryResolveUnityNativeDeclaredType (
            Type ownerType,
            string propertySegment,
            out Type declaredType)
        {
            if (string.Equals(propertySegment, "m_Script", StringComparison.Ordinal))
            {
                declaredType = typeof(MonoScript);
                return true;
            }

            if (string.Equals(propertySegment, "m_Enabled", StringComparison.Ordinal)
                && typeof(Behaviour).IsAssignableFrom(ownerType))
            {
                declaredType = typeof(bool);
                return true;
            }

            declaredType = null!;
            return false;
        }
    }
}
