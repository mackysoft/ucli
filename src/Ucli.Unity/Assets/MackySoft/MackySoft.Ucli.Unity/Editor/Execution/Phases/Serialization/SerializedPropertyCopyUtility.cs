using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Copies selected serialized properties between components with persistent-reference guards. </summary>
    internal static class SerializedPropertyCopyUtility
    {
        /// <summary> Copies selected serialized properties from one component to another component of the same effective schema. </summary>
        /// <param name="sourceComponent"> The component that supplies property values. </param>
        /// <param name="destinationComponent"> The component that receives property values. </param>
        /// <param name="changes"> The selected request-attributed property changes to copy. </param>
        /// <param name="destinationDescription"> A human-readable destination name used in validation errors. </param>
        /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when every selected property is copied and applied; otherwise <see langword="false" />. </returns>
        public static bool TryCopyComponentProperties (
            Component sourceComponent,
            Component destinationComponent,
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
            string destinationDescription,
            out string errorMessage)
        {
            var sourceObject = new SerializedObject(sourceComponent);
            var destinationObject = new SerializedObject(destinationComponent);
            sourceObject.UpdateIfRequiredOrScript();
            destinationObject.UpdateIfRequiredOrScript();
            for (var i = 0; i < changes.Count; i++)
            {
                var propertyPath = changes[i].PropertyPath;
                var sourceProperty = sourceObject.FindProperty(propertyPath);
                if (sourceProperty == null)
                {
                    errorMessage = $"SerializedProperty path was not found: {propertyPath}.";
                    return false;
                }

                if (destinationObject.FindProperty(propertyPath) == null)
                {
                    errorMessage = $"SerializedProperty path was not found in {destinationDescription}: {propertyPath}.";
                    return false;
                }

                if (!TryValidatePersistentObjectReferences(sourceProperty, destinationDescription, out errorMessage))
                {
                    return false;
                }

                destinationObject.CopyFromSerializedProperty(sourceProperty);
            }

            _ = destinationObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(destinationComponent);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryValidatePersistentObjectReferences (
            SerializedProperty sourceProperty,
            string destinationDescription,
            out string errorMessage)
        {
            if (!TryValidatePersistentObjectReference(sourceProperty, destinationDescription, out errorMessage))
            {
                return false;
            }

            var iterator = sourceProperty.Copy();
            var endProperty = iterator.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren)
                && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                if (!TryValidatePersistentObjectReference(iterator, destinationDescription, out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryValidatePersistentObjectReference (
            SerializedProperty property,
            string destinationDescription,
            out string errorMessage)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference
                && property.propertyType != SerializedPropertyType.ExposedReference)
            {
                errorMessage = string.Empty;
                return true;
            }

            var unityObject = property.propertyType == SerializedPropertyType.ExposedReference
                ? property.exposedReferenceValue
                : property.objectReferenceValue;
            if (unityObject == null
                || EditorUtility.IsPersistent(unityObject))
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = $"SerializedProperty path contains a non-persistent object reference that cannot be copied to {destinationDescription}: {property.propertyPath}.";
            return false;
        }
    }
}
