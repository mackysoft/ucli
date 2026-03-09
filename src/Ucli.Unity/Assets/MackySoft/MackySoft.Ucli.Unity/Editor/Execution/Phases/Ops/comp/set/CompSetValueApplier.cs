using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Unity.Index;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Applies validated <c>ucli.comp.set</c> assignments to one temporary component sandbox. </summary>
    internal static class CompSetValueApplier
    {
        public static bool TryApply (
            Component component,
            IReadOnlyList<CompSetAssignment> assignments,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out bool changed,
            out string errorMessage)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            var serializedObject = new SerializedObject(component);
            var rootType = component.GetType();
            changed = false;
            errorMessage = string.Empty;
            serializedObject.UpdateIfRequiredOrScript();
            for (var i = 0; i < assignments.Count; i++)
            {
                var assignment = assignments[i];
                var property = serializedObject.FindProperty(assignment.Path);
                if (property == null)
                {
                    errorMessage = $"SerializedProperty path was not found: {assignment.Path}.";
                    return false;
                }

                if (!TryApplyPropertyValue(
                    serializedObject,
                    rootType,
                    property,
                    assignment.Value,
                    executionContext,
                    allowTemporaryState,
                    assignment.Path,
                    out var assignmentChanged,
                    out errorMessage))
                {
                    return false;
                }

                changed |= assignmentChanged;
            }

            _ = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(component);
            }

            return true;
        }

        private static bool TryApplyPropertyValue (
            SerializedObject serializedObject,
            Type rootType,
            SerializedProperty property,
            JsonElement value,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            errorMessage = string.Empty;
            if (IsReadOnlyProperty(property))
            {
                errorMessage = $"SerializedProperty is read-only: {logicalPath}.";
                return false;
            }

            if (property.propertyType == SerializedPropertyType.FixedBufferSize)
            {
                return TryApplyFixedBufferSize(property, value, logicalPath, out changed, out errorMessage);
            }

            if (property.propertyType == SerializedPropertyType.ArraySize)
            {
                return TryApplyArraySize(property, value, logicalPath, out changed, out errorMessage);
            }

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                if (value.ValueKind != JsonValueKind.Array)
                {
                    errorMessage = $"SerializedProperty '{logicalPath}' must be set from a JSON array.";
                    return false;
                }

                return TryApplyArray(
                    serializedObject,
                    rootType,
                    property,
                    value,
                    executionContext,
                    allowTemporaryState,
                    logicalPath,
                    out changed,
                    out errorMessage);
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    return TryApplyInteger(serializedObject, property, rootType, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Boolean:
                    return TryApplyBoolean(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Float:
                    return TryApplyFloat(serializedObject, property, rootType, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.String:
                    return TryApplyString(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Color:
                    return TryApplyColor(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.ObjectReference:
                    return TryApplyObjectReference(property, value, executionContext, allowTemporaryState, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Enum:
                    return TryApplyEnum(serializedObject, property, rootType, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Vector2:
                    return TryApplyVector2(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Vector3:
                    return TryApplyVector3(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Vector4:
                    return TryApplyVector4(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Rect:
                    return TryApplyRect(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Character:
                    return TryApplyCharacter(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.AnimationCurve:
                    return TryApplyAnimationCurve(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Bounds:
                    return TryApplyBounds(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Gradient:
                    return TryApplyGradient(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Quaternion:
                    return TryApplyQuaternion(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.ExposedReference:
                    return TryApplyExposedReference(property, value, executionContext, allowTemporaryState, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Vector2Int:
                    return TryApplyVector2Int(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Vector3Int:
                    return TryApplyVector3Int(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.RectInt:
                    return TryApplyRectInt(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.BoundsInt:
                    return TryApplyBoundsInt(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.ManagedReference:
                    return TryApplyManagedReference(
                        serializedObject,
                        rootType,
                        property,
                        value,
                        executionContext,
                        allowTemporaryState,
                        logicalPath,
                        out changed,
                        out errorMessage);

                case SerializedPropertyType.Hash128:
                    return TryApplyHash128(property, value, logicalPath, out changed, out errorMessage);

                case SerializedPropertyType.Generic:
                    return TryApplyGenericObject(
                        serializedObject,
                        rootType,
                        property,
                        value,
                        executionContext,
                        allowTemporaryState,
                        logicalPath,
                        out changed,
                        out errorMessage);

                default:
                    errorMessage = $"SerializedProperty type is not supported by ucli.comp.set: {property.propertyType}.";
                    return false;
            }
        }

        private static bool TryApplyArray (
            SerializedObject serializedObject,
            Type rootType,
            SerializedProperty property,
            JsonElement value,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            errorMessage = string.Empty;
            var arrayLength = value.GetArrayLength();
            if (property.arraySize != arrayLength)
            {
                property.arraySize = arrayLength;
                changed = true;
            }

            var index = 0;
            foreach (var element in value.EnumerateArray())
            {
                var elementProperty = property.GetArrayElementAtIndex(index);
                if (!TryApplyPropertyValue(
                    serializedObject,
                    rootType,
                    elementProperty,
                    element,
                    executionContext,
                    allowTemporaryState,
                    $"{logicalPath}.Array.data[{index}]",
                    out var elementChanged,
                    out errorMessage))
                {
                    return false;
                }

                changed |= elementChanged;
                index++;
            }

            return true;
        }

        private static bool TryApplyInteger (
            SerializedObject serializedObject,
            SerializedProperty property,
            Type rootType,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryResolveDeclaredType(serializedObject, rootType, property, out var declaredType, out errorMessage))
            {
                return false;
            }

            declaredType = NormalizeNullableType(declaredType);
            if (declaredType == typeof(LayerMask))
            {
                if (!TryGetInt32(value, logicalPath, out var layerMaskValue, out errorMessage))
                {
                    return false;
                }

                changed = property.intValue != layerMaskValue;
                property.intValue = layerMaskValue;
                return true;
            }

            if (declaredType == typeof(long))
            {
                if (!TryGetInt64(value, logicalPath, out var longValue, out errorMessage))
                {
                    return false;
                }

                changed = property.longValue != longValue;
                property.longValue = longValue;
                return true;
            }

            if (declaredType == typeof(ulong))
            {
                if (!TryGetUInt64(value, logicalPath, out var ulongValue, out errorMessage))
                {
                    return false;
                }

                changed = property.ulongValue != ulongValue;
                property.ulongValue = ulongValue;
                return true;
            }

            if (declaredType == typeof(uint))
            {
                if (!TryGetUInt64(value, logicalPath, out var uintValue, out errorMessage) || uintValue > uint.MaxValue)
                {
                    errorMessage = $"SerializedProperty '{logicalPath}' must be a 32-bit unsigned integer.";
                    return false;
                }

                changed = property.longValue != (long)uintValue;
                property.longValue = (long)uintValue;
                return true;
            }

            if (declaredType == typeof(short)
                || declaredType == typeof(ushort)
                || declaredType == typeof(byte)
                || declaredType == typeof(sbyte)
                || declaredType == typeof(int))
            {
                if (!TryGetInt32(value, logicalPath, out var intValue, out errorMessage))
                {
                    return false;
                }

                changed = property.intValue != intValue;
                property.intValue = intValue;
                return true;
            }

            errorMessage = $"SerializedProperty '{logicalPath}' integer type is not supported: {declaredType.FullName}.";
            return false;
        }

        private static bool TryApplyBoolean (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' must be a boolean.";
                return false;
            }

            var booleanValue = value.GetBoolean();
            changed = property.boolValue != booleanValue;
            property.boolValue = booleanValue;
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryApplyFloat (
            SerializedObject serializedObject,
            SerializedProperty property,
            Type rootType,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryResolveDeclaredType(serializedObject, rootType, property, out var declaredType, out errorMessage))
            {
                return false;
            }

            declaredType = NormalizeNullableType(declaredType);
            if (declaredType == typeof(double))
            {
                if (!TryGetDouble(value, logicalPath, out var doubleValue, out errorMessage))
                {
                    return false;
                }

                changed = property.doubleValue != doubleValue;
                property.doubleValue = doubleValue;
                return true;
            }

            if (!TryGetSingle(value, logicalPath, out var floatValue, out errorMessage))
            {
                return false;
            }

            changed = !Mathf.Approximately(property.floatValue, floatValue);
            property.floatValue = floatValue;
            return true;
        }

        private static bool TryApplyString (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (value.ValueKind != JsonValueKind.String)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' must be a string.";
                return false;
            }

            var stringValue = value.GetString() ?? string.Empty;
            changed = !string.Equals(property.stringValue, stringValue, StringComparison.Ordinal);
            property.stringValue = stringValue;
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryApplyColor (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseColor(value, logicalPath, out var color, out errorMessage))
            {
                return false;
            }

            changed = property.colorValue != color;
            property.colorValue = color;
            return true;
        }

        private static bool TryApplyObjectReference (
            SerializedProperty property,
            JsonElement value,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryResolveUnityObjectValue(value, executionContext, allowTemporaryState, logicalPath, out var unityObject, out errorMessage))
            {
                return false;
            }

            var current = property.objectReferenceValue;
            changed = current != unityObject;
            property.objectReferenceValue = unityObject;
            return true;
        }

        private static bool TryApplyEnum (
            SerializedObject serializedObject,
            SerializedProperty property,
            Type rootType,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryResolveDeclaredType(serializedObject, rootType, property, out var declaredType, out errorMessage))
            {
                return false;
            }

            declaredType = NormalizeNullableType(declaredType);
            if (!declaredType.IsEnum)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' declared type is not an enum.";
                return false;
            }

            if (!TryParseEnumRawValue(declaredType, value, logicalPath, out var rawValue, out errorMessage))
            {
                return false;
            }

            changed = property.intValue != rawValue;
            property.intValue = rawValue;
            return true;
        }

        private static bool TryApplyVector2 (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseVector2(value, logicalPath, out var vector, out errorMessage))
            {
                return false;
            }

            changed = property.vector2Value != vector;
            property.vector2Value = vector;
            return true;
        }

        private static bool TryApplyVector3 (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseVector3(value, logicalPath, out var vector, out errorMessage))
            {
                return false;
            }

            changed = property.vector3Value != vector;
            property.vector3Value = vector;
            return true;
        }

        private static bool TryApplyVector4 (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseVector4(value, logicalPath, out var vector, out errorMessage))
            {
                return false;
            }

            changed = property.vector4Value != vector;
            property.vector4Value = vector;
            return true;
        }

        private static bool TryApplyRect (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseRect(value, logicalPath, out var rect, out errorMessage))
            {
                return false;
            }

            changed = property.rectValue != rect;
            property.rectValue = rect;
            return true;
        }

        private static bool TryApplyCharacter (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseCharacter(value, logicalPath, out var character, out errorMessage))
            {
                return false;
            }

            changed = property.intValue != character;
            property.intValue = character;
            return true;
        }

        private static bool TryApplyAnimationCurve (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseAnimationCurve(value, logicalPath, out var curve, out errorMessage))
            {
                return false;
            }

            changed = !AreAnimationCurvesEqual(property.animationCurveValue, curve);
            property.animationCurveValue = curve;
            return true;
        }

        private static bool TryApplyBounds (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseBounds(value, logicalPath, out var bounds, out errorMessage))
            {
                return false;
            }

            changed = property.boundsValue != bounds;
            property.boundsValue = bounds;
            return true;
        }

        private static bool TryApplyGradient (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseGradient(value, logicalPath, out var gradient, out errorMessage))
            {
                return false;
            }

            changed = !AreGradientsEqual(property.gradientValue, gradient);
            property.gradientValue = gradient;
            return true;
        }

        private static bool TryApplyQuaternion (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseQuaternion(value, logicalPath, out var quaternion, out errorMessage))
            {
                return false;
            }

            changed = property.quaternionValue != quaternion;
            property.quaternionValue = quaternion;
            return true;
        }

        private static bool TryApplyExposedReference (
            SerializedProperty property,
            JsonElement value,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryResolveUnityObjectValue(value, executionContext, allowTemporaryState, logicalPath, out var unityObject, out errorMessage))
            {
                return false;
            }

            var current = property.exposedReferenceValue;
            changed = current != unityObject;
            property.exposedReferenceValue = unityObject;
            return true;
        }

        private static bool TryApplyVector2Int (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseVector2Int(value, logicalPath, out var vector, out errorMessage))
            {
                return false;
            }

            changed = property.vector2IntValue != vector;
            property.vector2IntValue = vector;
            return true;
        }

        private static bool TryApplyVector3Int (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseVector3Int(value, logicalPath, out var vector, out errorMessage))
            {
                return false;
            }

            changed = property.vector3IntValue != vector;
            property.vector3IntValue = vector;
            return true;
        }

        private static bool TryApplyRectInt (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseRectInt(value, logicalPath, out var rect, out errorMessage))
            {
                return false;
            }

            changed = property.rectIntValue != rect;
            property.rectIntValue = rect;
            return true;
        }

        private static bool TryApplyBoundsInt (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryParseBoundsInt(value, logicalPath, out var bounds, out errorMessage))
            {
                return false;
            }

            changed = property.boundsIntValue != bounds;
            property.boundsIntValue = bounds;
            return true;
        }

        private static bool TryApplyManagedReference (
            SerializedObject serializedObject,
            Type rootType,
            SerializedProperty property,
            JsonElement value,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            errorMessage = string.Empty;
            if (!TryResolveDeclaredType(serializedObject, rootType, property, out var declaredType, out errorMessage))
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.Null)
            {
                changed = property.managedReferenceValue != null;
                property.managedReferenceValue = null;
                return true;
            }

            if (!TryParseManagedReferenceContract(value, logicalPath, out var typeId, out var payload, out errorMessage))
            {
                return false;
            }

            if (!ComponentTypeResolver.TryResolveRuntimeType(typeId!, out var runtimeType, out errorMessage))
            {
                errorMessage = $"Managed reference typeId is invalid for '{logicalPath}'. {errorMessage}";
                return false;
            }

            if (!declaredType.IsAssignableFrom(runtimeType))
            {
                errorMessage = $"Managed reference type '{runtimeType!.FullName}' is not assignable to '{declaredType.FullName}'.";
                return false;
            }

            var constructor = runtimeType!.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                errorMessage = $"Managed reference type '{runtimeType.FullName}' must expose a parameterless constructor.";
                return false;
            }

            var currentValue = property.managedReferenceValue;
            var typeChanged = currentValue == null || currentValue.GetType() != runtimeType;
            property.managedReferenceValue = constructor.Invoke(Array.Empty<object>());
            _ = serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            var refreshedProperty = serializedObject.FindProperty(property.propertyPath);
            if (refreshedProperty == null)
            {
                errorMessage = $"SerializedProperty path was not found after managed reference update: {logicalPath}.";
                return false;
            }

            if (!TryApplyGenericObject(
                serializedObject,
                rootType,
                refreshedProperty,
                payload!.Value,
                executionContext,
                allowTemporaryState,
                logicalPath,
                out var payloadChanged,
                out errorMessage))
            {
                return false;
            }

            changed = typeChanged || payloadChanged;
            return true;
        }

        private static bool TryApplyHash128 (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (value.ValueKind != JsonValueKind.String)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' must be a string.";
                return false;
            }

            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text)
                || text.Length != 32
                || !IsHexString(text))
            {
                errorMessage = $"SerializedProperty '{logicalPath}' must be a valid Hash128 string.";
                return false;
            }

            var hash = Hash128.Parse(text);
            changed = property.hash128Value != hash;
            property.hash128Value = hash;
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryApplyGenericObject (
            SerializedObject serializedObject,
            Type rootType,
            SerializedProperty property,
            JsonElement value,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            errorMessage = string.Empty;
            if (value.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' must be set from a JSON object.";
                return false;
            }

            foreach (var child in value.EnumerateObject())
            {
                var childProperty = property.FindPropertyRelative(child.Name);
                if (childProperty == null)
                {
                    errorMessage = $"SerializedProperty child was not found: {logicalPath}.{child.Name}.";
                    return false;
                }

                if (!TryApplyPropertyValue(
                    serializedObject,
                    rootType,
                    childProperty,
                    child.Value,
                    executionContext,
                    allowTemporaryState,
                    $"{logicalPath}.{child.Name}",
                    out var childChanged,
                    out errorMessage))
                {
                    return false;
                }

                changed |= childChanged;
            }

            return true;
        }

        private static bool TryApplyArraySize (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            changed = false;
            if (!TryGetInt32(value, logicalPath, out var intValue, out errorMessage))
            {
                return false;
            }

            if (intValue < 0)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' must be greater than or equal to 0.";
                return false;
            }

            changed = property.intValue != intValue;
            property.intValue = intValue;
            return true;
        }

        private static bool TryApplyFixedBufferSize (
            SerializedProperty property,
            JsonElement value,
            string logicalPath,
            out bool changed,
            out string errorMessage)
        {
            return TryApplyArraySize(property, value, logicalPath, out changed, out errorMessage);
        }

        private static bool TryResolveDeclaredType (
            SerializedObject serializedObject,
            Type rootType,
            SerializedProperty property,
            out Type declaredType,
            out string errorMessage)
        {
            if (property.propertyType == SerializedPropertyType.ArraySize
                || property.propertyType == SerializedPropertyType.FixedBufferSize)
            {
                declaredType = typeof(int);
                errorMessage = string.Empty;
                return true;
            }

            var resolution = IndexDeclaredTypeResolver.Resolve(rootType, property.propertyPath);
            if (!resolution.IsResolved)
            {
                if (TryResolveManagedReferenceDeclaredType(serializedObject, property, out declaredType, out errorMessage))
                {
                    return true;
                }

                declaredType = null!;
                errorMessage = $"Declared type could not be resolved for SerializedProperty path '{property.propertyPath}'.";
                return false;
            }

            declaredType = resolution.DeclaredType;
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveManagedReferenceDeclaredType (
            SerializedObject serializedObject,
            SerializedProperty property,
            out Type declaredType,
            out string errorMessage)
        {
            var propertyPath = property.propertyPath;
            var originalPropertyPath = propertyPath;
            while (TryGetParentPropertyPath(propertyPath, out var parentPath, out var relativePath))
            {
                var parentProperty = serializedObject.FindProperty(parentPath);
                if (parentProperty != null
                    && parentProperty.propertyType == SerializedPropertyType.ManagedReference
                    && parentProperty.managedReferenceValue != null)
                {
                    relativePath = originalPropertyPath.Substring(parentPath.Length + 1);
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

        private static bool TryGetParentPropertyPath (
            string propertyPath,
            out string parentPath,
            out string relativePath)
        {
            var parentSeparatorIndex = propertyPath.LastIndexOf('.');
            if (parentSeparatorIndex < 0)
            {
                parentPath = string.Empty;
                relativePath = string.Empty;
                return false;
            }

            parentPath = propertyPath.Substring(0, parentSeparatorIndex);
            relativePath = propertyPath.Substring(parentSeparatorIndex + 1);
            return true;
        }

        private static bool IsReadOnlyProperty (SerializedProperty property)
        {
            return !property.editable
                || string.Equals(property.propertyPath, "m_Script", StringComparison.Ordinal);
        }

        private static bool TryResolveUnityObjectValue (
            JsonElement value,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            string logicalPath,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (value.ValueKind == JsonValueKind.Null)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (!UnityObjectReferenceCodec.TryParse(value, logicalPath, out var reference, out errorMessage))
            {
                return false;
            }

            return ComponentOperationUtilities.TryResolveUnityObject(
                reference,
                executionContext,
                allowTemporaryState,
                out unityObject,
                out errorMessage);
        }

        private static Type NormalizeNullableType (Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        private static bool TryGetInt32 (
            JsonElement value,
            string logicalPath,
            out int result,
            out string errorMessage)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result))
            {
                errorMessage = string.Empty;
                return true;
            }

            result = default;
            errorMessage = $"SerializedProperty '{logicalPath}' must be a 32-bit integer.";
            return false;
        }

        private static bool TryGetInt64 (
            JsonElement value,
            string logicalPath,
            out long result,
            out string errorMessage)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out result))
            {
                errorMessage = string.Empty;
                return true;
            }

            result = default;
            errorMessage = $"SerializedProperty '{logicalPath}' must be a 64-bit integer.";
            return false;
        }

        private static bool TryGetUInt64 (
            JsonElement value,
            string logicalPath,
            out ulong result,
            out string errorMessage)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out result))
            {
                errorMessage = string.Empty;
                return true;
            }

            result = default;
            errorMessage = $"SerializedProperty '{logicalPath}' must be an unsigned integer.";
            return false;
        }

        private static bool TryGetSingle (
            JsonElement value,
            string logicalPath,
            out float result,
            out string errorMessage)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out result))
            {
                errorMessage = string.Empty;
                return true;
            }

            result = default;
            errorMessage = $"SerializedProperty '{logicalPath}' must be a number.";
            return false;
        }

        private static bool TryGetDouble (
            JsonElement value,
            string logicalPath,
            out double result,
            out string errorMessage)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out result))
            {
                errorMessage = string.Empty;
                return true;
            }

            result = default;
            errorMessage = $"SerializedProperty '{logicalPath}' must be a number.";
            return false;
        }

        private static bool TryParseEnumRawValue (
            Type enumType,
            JsonElement value,
            string logicalPath,
            out int rawValue,
            out string errorMessage)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                var name = value.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    rawValue = default;
                    errorMessage = $"SerializedProperty '{logicalPath}' enum name must not be empty.";
                    return false;
                }

                if (!Enum.IsDefined(enumType, name))
                {
                    rawValue = default;
                    errorMessage = $"SerializedProperty '{logicalPath}' enum name is invalid: {name}.";
                    return false;
                }

                rawValue = Convert.ToInt32(Enum.Parse(enumType, name, ignoreCase: false));
                errorMessage = string.Empty;
                return true;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out rawValue))
            {
                errorMessage = string.Empty;
                return true;
            }

            rawValue = default;
            errorMessage = $"SerializedProperty '{logicalPath}' enum value must be a string name or integer literal.";
            return false;
        }

        private static bool TryParseColor (
            JsonElement value,
            string logicalPath,
            out Color color,
            out string errorMessage)
        {
            color = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredNumber(properties, "r", logicalPath, out var r, out errorMessage)
                || !TryGetRequiredNumber(properties, "g", logicalPath, out var g, out errorMessage)
                || !TryGetRequiredNumber(properties, "b", logicalPath, out var b, out errorMessage)
                || !TryGetRequiredNumber(properties, "a", logicalPath, out var a, out errorMessage))
            {
                return false;
            }

            color = new Color(r, g, b, a);
            return true;
        }

        private static bool TryParseVector2 (
            JsonElement value,
            string logicalPath,
            out Vector2 vector,
            out string errorMessage)
        {
            vector = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredNumber(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredNumber(properties, "y", logicalPath, out var y, out errorMessage))
            {
                return false;
            }

            vector = new Vector2(x, y);
            return true;
        }

        private static bool TryParseVector3 (
            JsonElement value,
            string logicalPath,
            out Vector3 vector,
            out string errorMessage)
        {
            vector = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredNumber(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredNumber(properties, "y", logicalPath, out var y, out errorMessage)
                || !TryGetRequiredNumber(properties, "z", logicalPath, out var z, out errorMessage))
            {
                return false;
            }

            vector = new Vector3(x, y, z);
            return true;
        }

        private static bool TryParseVector4 (
            JsonElement value,
            string logicalPath,
            out Vector4 vector,
            out string errorMessage)
        {
            vector = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredNumber(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredNumber(properties, "y", logicalPath, out var y, out errorMessage)
                || !TryGetRequiredNumber(properties, "z", logicalPath, out var z, out errorMessage)
                || !TryGetRequiredNumber(properties, "w", logicalPath, out var w, out errorMessage))
            {
                return false;
            }

            vector = new Vector4(x, y, z, w);
            return true;
        }

        private static bool TryParseRect (
            JsonElement value,
            string logicalPath,
            out Rect rect,
            out string errorMessage)
        {
            rect = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredNumber(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredNumber(properties, "y", logicalPath, out var y, out errorMessage)
                || !TryGetRequiredNumber(properties, "width", logicalPath, out var width, out errorMessage)
                || !TryGetRequiredNumber(properties, "height", logicalPath, out var height, out errorMessage))
            {
                return false;
            }

            rect = new Rect(x, y, width, height);
            return true;
        }

        private static bool TryParseCharacter (
            JsonElement value,
            string logicalPath,
            out int character,
            out string errorMessage)
        {
            character = default;
            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (string.IsNullOrEmpty(text) || text.Length != 1)
                {
                    errorMessage = $"SerializedProperty '{logicalPath}' must be a single-character string.";
                    return false;
                }

                character = text[0];
                errorMessage = string.Empty;
                return true;
            }

            return TryGetInt32(value, logicalPath, out character, out errorMessage);
        }

        private static bool TryParseAnimationCurve (
            JsonElement value,
            string logicalPath,
            out AnimationCurve curve,
            out string errorMessage)
        {
            curve = new AnimationCurve();
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredArray(properties, "keys", logicalPath, out var keysElement, out errorMessage)
                || !TryGetRequiredEnum<WrapMode>(properties, "preWrapMode", logicalPath, out var preWrapMode, out errorMessage)
                || !TryGetRequiredEnum<WrapMode>(properties, "postWrapMode", logicalPath, out var postWrapMode, out errorMessage))
            {
                return false;
            }

            var keyframes = new List<Keyframe>();
            foreach (var keyElement in keysElement.EnumerateArray())
            {
                if (!TryReadObject(keyElement, logicalPath, out var keyProperties, out errorMessage))
                {
                    return false;
                }

                if (!TryGetRequiredNumber(keyProperties, "time", logicalPath, out var time, out errorMessage)
                    || !TryGetRequiredNumber(keyProperties, "value", logicalPath, out var keyValue, out errorMessage))
                {
                    return false;
                }

                var keyframe = new Keyframe(time, keyValue);
                if (TryGetOptionalNumber(keyProperties, "inTangent", out var inTangent))
                {
                    keyframe.inTangent = inTangent;
                }

                if (TryGetOptionalNumber(keyProperties, "outTangent", out var outTangent))
                {
                    keyframe.outTangent = outTangent;
                }

                if (TryGetOptionalNumber(keyProperties, "inWeight", out var inWeight))
                {
                    keyframe.inWeight = inWeight;
                }

                if (TryGetOptionalNumber(keyProperties, "outWeight", out var outWeight))
                {
                    keyframe.outWeight = outWeight;
                }

                if (TryGetOptionalEnum<WeightedMode>(keyProperties, "weightedMode", out var weightedMode, out errorMessage))
                {
                    keyframe.weightedMode = weightedMode;
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                {
                    return false;
                }

                keyframes.Add(keyframe);
            }

            curve = new AnimationCurve(keyframes.ToArray())
            {
                preWrapMode = preWrapMode,
                postWrapMode = postWrapMode,
            };
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryParseBounds (
            JsonElement value,
            string logicalPath,
            out Bounds bounds,
            out string errorMessage)
        {
            bounds = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredObject(properties, "center", logicalPath, out var centerElement, out errorMessage)
                || !TryGetRequiredObject(properties, "size", logicalPath, out var sizeElement, out errorMessage)
                || !TryParseVector3(centerElement, $"{logicalPath}.center", out var center, out errorMessage)
                || !TryParseVector3(sizeElement, $"{logicalPath}.size", out var size, out errorMessage))
            {
                return false;
            }

            bounds = new Bounds(center, size);
            return true;
        }

        private static bool TryParseGradient (
            JsonElement value,
            string logicalPath,
            out Gradient gradient,
            out string errorMessage)
        {
            gradient = new Gradient();
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredArray(properties, "colorKeys", logicalPath, out var colorKeysElement, out errorMessage)
                || !TryGetRequiredArray(properties, "alphaKeys", logicalPath, out var alphaKeysElement, out errorMessage)
                || !TryGetRequiredEnum<GradientMode>(properties, "mode", logicalPath, out var mode, out errorMessage))
            {
                return false;
            }

            var colorKeys = new List<GradientColorKey>();
            foreach (var colorKeyElement in colorKeysElement.EnumerateArray())
            {
                if (!TryReadObject(colorKeyElement, logicalPath, out var colorKeyProperties, out errorMessage))
                {
                    return false;
                }

                if (!TryGetRequiredObject(colorKeyProperties, "color", logicalPath, out var colorElement, out errorMessage)
                    || !TryGetRequiredNumber(colorKeyProperties, "time", logicalPath, out var time, out errorMessage)
                    || !TryParseColor(colorElement, $"{logicalPath}.color", out var color, out errorMessage))
                {
                    return false;
                }

                colorKeys.Add(new GradientColorKey(color, time));
            }

            var alphaKeys = new List<GradientAlphaKey>();
            foreach (var alphaKeyElement in alphaKeysElement.EnumerateArray())
            {
                if (!TryReadObject(alphaKeyElement, logicalPath, out var alphaKeyProperties, out errorMessage))
                {
                    return false;
                }

                if (!TryGetRequiredNumber(alphaKeyProperties, "alpha", logicalPath, out var alpha, out errorMessage)
                    || !TryGetRequiredNumber(alphaKeyProperties, "time", logicalPath, out var time, out errorMessage))
                {
                    return false;
                }

                alphaKeys.Add(new GradientAlphaKey(alpha, time));
            }

            gradient.mode = mode;
            gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryParseQuaternion (
            JsonElement value,
            string logicalPath,
            out Quaternion quaternion,
            out string errorMessage)
        {
            quaternion = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredNumber(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredNumber(properties, "y", logicalPath, out var y, out errorMessage)
                || !TryGetRequiredNumber(properties, "z", logicalPath, out var z, out errorMessage)
                || !TryGetRequiredNumber(properties, "w", logicalPath, out var w, out errorMessage))
            {
                return false;
            }

            quaternion = new Quaternion(x, y, z, w);
            return true;
        }

        private static bool TryParseVector2Int (
            JsonElement value,
            string logicalPath,
            out Vector2Int vector,
            out string errorMessage)
        {
            vector = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredInt(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredInt(properties, "y", logicalPath, out var y, out errorMessage))
            {
                return false;
            }

            vector = new Vector2Int(x, y);
            return true;
        }

        private static bool TryParseVector3Int (
            JsonElement value,
            string logicalPath,
            out Vector3Int vector,
            out string errorMessage)
        {
            vector = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredInt(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredInt(properties, "y", logicalPath, out var y, out errorMessage)
                || !TryGetRequiredInt(properties, "z", logicalPath, out var z, out errorMessage))
            {
                return false;
            }

            vector = new Vector3Int(x, y, z);
            return true;
        }

        private static bool TryParseRectInt (
            JsonElement value,
            string logicalPath,
            out RectInt rect,
            out string errorMessage)
        {
            rect = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredInt(properties, "x", logicalPath, out var x, out errorMessage)
                || !TryGetRequiredInt(properties, "y", logicalPath, out var y, out errorMessage)
                || !TryGetRequiredInt(properties, "width", logicalPath, out var width, out errorMessage)
                || !TryGetRequiredInt(properties, "height", logicalPath, out var height, out errorMessage))
            {
                return false;
            }

            rect = new RectInt(x, y, width, height);
            return true;
        }

        private static bool TryParseBoundsInt (
            JsonElement value,
            string logicalPath,
            out BoundsInt bounds,
            out string errorMessage)
        {
            bounds = default;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!TryGetRequiredObject(properties, "position", logicalPath, out var positionElement, out errorMessage)
                || !TryGetRequiredObject(properties, "size", logicalPath, out var sizeElement, out errorMessage)
                || !TryParseVector3Int(positionElement, $"{logicalPath}.position", out var position, out errorMessage)
                || !TryParseVector3Int(sizeElement, $"{logicalPath}.size", out var size, out errorMessage))
            {
                return false;
            }

            bounds = new BoundsInt(position, size);
            return true;
        }

        private static bool TryParseManagedReferenceContract (
            JsonElement value,
            string logicalPath,
            out string? typeId,
            out JsonElement? payload,
            out string errorMessage)
        {
            typeId = null;
            payload = null;
            errorMessage = string.Empty;
            if (!TryReadObject(value, logicalPath, out var properties, out errorMessage))
            {
                return false;
            }

            if (!properties.TryGetValue("type", out var typeElement)
                || !OperationArgumentValueReader.TryReadRequiredString(
                    typeElement,
                    $"{logicalPath}.type",
                    expectedTypeDescription: "a string",
                    out typeId,
                    out errorMessage))
            {
                return false;
            }

            if (!properties.TryGetValue("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Managed reference '{logicalPath}' requires object property 'value'.";
                return false;
            }

            payload = valueElement;
            return true;
        }

        private static bool TryReadObject (
            JsonElement value,
            string logicalPath,
            out Dictionary<string, JsonElement> properties,
            out string errorMessage)
        {
            properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (value.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' must be a JSON object.";
                return false;
            }

            foreach (var property in value.EnumerateObject())
            {
                properties[property.Name] = property.Value;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryGetRequiredNumber (
            IReadOnlyDictionary<string, JsonElement> properties,
            string name,
            string logicalPath,
            out float result,
            out string errorMessage)
        {
            result = default;
            if (!properties.TryGetValue(name, out var element))
            {
                errorMessage = $"SerializedProperty '{logicalPath}' requires property '{name}'.";
                return false;
            }

            return TryGetSingle(element, $"{logicalPath}.{name}", out result, out errorMessage);
        }

        private static bool TryGetRequiredInt (
            IReadOnlyDictionary<string, JsonElement> properties,
            string name,
            string logicalPath,
            out int result,
            out string errorMessage)
        {
            result = default;
            if (!properties.TryGetValue(name, out var element))
            {
                errorMessage = $"SerializedProperty '{logicalPath}' requires property '{name}'.";
                return false;
            }

            return TryGetInt32(element, $"{logicalPath}.{name}", out result, out errorMessage);
        }

        private static bool TryGetRequiredObject (
            IReadOnlyDictionary<string, JsonElement> properties,
            string name,
            string logicalPath,
            out JsonElement element,
            out string errorMessage)
        {
            element = default;
            if (!properties.TryGetValue(name, out element) || element.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' requires object property '{name}'.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryGetRequiredArray (
            IReadOnlyDictionary<string, JsonElement> properties,
            string name,
            string logicalPath,
            out JsonElement element,
            out string errorMessage)
        {
            element = default;
            if (!properties.TryGetValue(name, out element) || element.ValueKind != JsonValueKind.Array)
            {
                errorMessage = $"SerializedProperty '{logicalPath}' requires array property '{name}'.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryGetRequiredEnum<TEnum> (
            IReadOnlyDictionary<string, JsonElement> properties,
            string name,
            string logicalPath,
            out TEnum value,
            out string errorMessage)
            where TEnum : struct, Enum
        {
            value = default;
            if (!properties.TryGetValue(name, out var element))
            {
                errorMessage = $"SerializedProperty '{logicalPath}' requires property '{name}'.";
                return false;
            }

            return TryParseEnumValue(element, $"{logicalPath}.{name}", out value, out errorMessage);
        }

        private static bool TryGetOptionalNumber (
            IReadOnlyDictionary<string, JsonElement> properties,
            string name,
            out float value)
        {
            value = default;
            if (!properties.TryGetValue(name, out var element) || element.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            return element.TryGetSingle(out value);
        }

        private static bool TryGetOptionalEnum<TEnum> (
            IReadOnlyDictionary<string, JsonElement> properties,
            string name,
            out TEnum value,
            out string errorMessage)
            where TEnum : struct, Enum
        {
            value = default;
            errorMessage = string.Empty;
            if (!properties.TryGetValue(name, out var element))
            {
                return false;
            }

            return TryParseEnumValue(element, name, out value, out errorMessage);
        }

        private static bool TryParseEnumValue<TEnum> (
            JsonElement value,
            string logicalPath,
            out TEnum enumValue,
            out string errorMessage)
            where TEnum : struct, Enum
        {
            enumValue = default;
            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!Enum.TryParse<TEnum>(text, ignoreCase: false, out enumValue))
                {
                    errorMessage = $"SerializedProperty '{logicalPath}' enum name is invalid: {text}.";
                    return false;
                }

                errorMessage = string.Empty;
                return true;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var rawValue))
            {
                enumValue = (TEnum)Enum.ToObject(typeof(TEnum), rawValue);
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = $"SerializedProperty '{logicalPath}' enum value must be a string name or integer literal.";
            return false;
        }

        private static bool IsHexString (string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                var isHexDigit =
                    (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');
                if (!isHexDigit)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreAnimationCurvesEqual (
            AnimationCurve? left,
            AnimationCurve? right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            if (left.preWrapMode != right.preWrapMode || left.postWrapMode != right.postWrapMode)
            {
                return false;
            }

            var leftKeys = left.keys;
            var rightKeys = right.keys;
            if (leftKeys.Length != rightKeys.Length)
            {
                return false;
            }

            for (var i = 0; i < leftKeys.Length; i++)
            {
                if (!leftKeys[i].Equals(rightKeys[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreGradientsEqual (
            Gradient? left,
            Gradient? right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            if (left.mode != right.mode)
            {
                return false;
            }

            var leftColorKeys = left.colorKeys;
            var rightColorKeys = right.colorKeys;
            if (leftColorKeys.Length != rightColorKeys.Length)
            {
                return false;
            }

            for (var i = 0; i < leftColorKeys.Length; i++)
            {
                if (!leftColorKeys[i].Equals(rightColorKeys[i]))
                {
                    return false;
                }
            }

            var leftAlphaKeys = left.alphaKeys;
            var rightAlphaKeys = right.alphaKeys;
            if (leftAlphaKeys.Length != rightAlphaKeys.Length)
            {
                return false;
            }

            for (var i = 0; i < leftAlphaKeys.Length; i++)
            {
                if (!leftAlphaKeys[i].Equals(rightAlphaKeys[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}