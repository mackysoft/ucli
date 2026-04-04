using System;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides shared helpers for building and repairing one mirrored preview object graph from a live source graph. </summary>
    internal static class TemporaryMirrorUtilities
    {
        public static bool TryRegisterHierarchyMirror (
            GameObject sourceRoot,
            GameObject previewRoot,
            TemporaryMirrorMapping mirrorMapping,
            out string errorMessage)
        {
            if (sourceRoot == null)
            {
                throw new ArgumentNullException(nameof(sourceRoot));
            }

            if (previewRoot == null)
            {
                throw new ArgumentNullException(nameof(previewRoot));
            }

            if (mirrorMapping == null)
            {
                throw new ArgumentNullException(nameof(mirrorMapping));
            }

            mirrorMapping.AddObjectPair(sourceRoot, previewRoot);

            var sourceComponents = sourceRoot.GetComponents<Component>();
            var previewComponents = previewRoot.GetComponents<Component>();
            if (sourceComponents.Length != previewComponents.Length)
            {
                errorMessage = $"Mirrored hierarchy diverged while registering component mapping at '{sourceRoot.name}'.";
                return false;
            }

            for (var i = 0; i < sourceComponents.Length; i++)
            {
                var sourceComponent = sourceComponents[i];
                var previewComponent = previewComponents[i];
                if (sourceComponent == null || previewComponent == null)
                {
                    continue;
                }

                if (sourceComponent.GetType() != previewComponent.GetType())
                {
                    errorMessage = $"Mirrored hierarchy diverged while registering component type mapping at '{sourceRoot.name}'.";
                    return false;
                }

                mirrorMapping.AddComponentPair(sourceComponent, previewComponent);
            }

            if (sourceRoot.transform.childCount != previewRoot.transform.childCount)
            {
                errorMessage = $"Mirrored hierarchy diverged while registering child mapping at '{sourceRoot.name}'.";
                return false;
            }

            for (var childIndex = 0; childIndex < sourceRoot.transform.childCount; childIndex++)
            {
                var sourceChild = sourceRoot.transform.GetChild(childIndex).gameObject;
                var previewChild = previewRoot.transform.GetChild(childIndex).gameObject;
                if (!TryRegisterHierarchyMirror(sourceChild, previewChild, mirrorMapping, out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool TryRebindMirroredLocalReferences (
            TemporaryMirrorMapping mirrorMapping,
            out string errorMessage)
        {
            if (mirrorMapping == null)
            {
                throw new ArgumentNullException(nameof(mirrorMapping));
            }

            var componentPairs = mirrorMapping.ComponentPairs;
            for (var pairIndex = 0; pairIndex < componentPairs.Count; pairIndex++)
            {
                var pair = componentPairs[pairIndex];
                if (!TryRebindMirroredComponentReferences(pair.SourceComponent, pair.PreviewComponent, mirrorMapping, out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        public static void RegisterStableSourceHierarchyBestEffort (
            GameObject sourceRoot,
            GameObject stableRoot,
            TemporaryMirrorMapping mirrorMapping)
        {
            if (sourceRoot == null)
            {
                throw new ArgumentNullException(nameof(sourceRoot));
            }

            if (stableRoot == null)
            {
                throw new ArgumentNullException(nameof(stableRoot));
            }

            if (mirrorMapping == null)
            {
                throw new ArgumentNullException(nameof(mirrorMapping));
            }

            mirrorMapping.AddStableSourcePair(sourceRoot, stableRoot);

            var sourceComponents = sourceRoot.GetComponents<Component>();
            var stableComponents = stableRoot.GetComponents<Component>();
            var componentCount = Mathf.Min(sourceComponents.Length, stableComponents.Length);
            for (var i = 0; i < componentCount; i++)
            {
                var sourceComponent = sourceComponents[i];
                var stableComponent = stableComponents[i];
                if (sourceComponent == null || stableComponent == null)
                {
                    continue;
                }

                if (sourceComponent.GetType() != stableComponent.GetType())
                {
                    break;
                }

                mirrorMapping.AddStableSourcePair(sourceComponent, stableComponent);
            }

            var childCount = Mathf.Min(sourceRoot.transform.childCount, stableRoot.transform.childCount);
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                var sourceChild = sourceRoot.transform.GetChild(childIndex).gameObject;
                var stableChild = stableRoot.transform.GetChild(childIndex).gameObject;
                RegisterStableSourceHierarchyBestEffort(sourceChild, stableChild, mirrorMapping);
            }
        }

        private static bool TryRebindMirroredComponentReferences (
            Component sourceComponent,
            Component previewComponent,
            TemporaryMirrorMapping mirrorMapping,
            out string errorMessage)
        {
            if (sourceComponent == null)
            {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            if (previewComponent == null)
            {
                throw new ArgumentNullException(nameof(previewComponent));
            }

            var sourceObject = new SerializedObject(sourceComponent);
            var previewObject = new SerializedObject(previewComponent);
            var sourceIterator = sourceObject.GetIterator();
            var previewIterator = previewObject.GetIterator();
            var sourceHasProperty = sourceIterator.Next(true);
            var previewHasProperty = previewIterator.Next(true);
            var changed = false;
            while (sourceHasProperty && previewHasProperty)
            {
                if (!string.Equals(sourceIterator.propertyPath, previewIterator.propertyPath, StringComparison.Ordinal)
                    || sourceIterator.propertyType != previewIterator.propertyType)
                {
                    errorMessage = $"Mirrored serialized state diverged while rebinding '{sourceComponent.GetType().Name}'.";
                    return false;
                }

                if (!ShouldSkipInternalObjectReferenceProperty(previewIterator)
                    && TryGetMirroredReferenceValue(sourceIterator, mirrorMapping, out var mirroredReference))
                {
                    switch (previewIterator.propertyType)
                    {
                        case SerializedPropertyType.ObjectReference:
                            previewIterator.objectReferenceValue = mirroredReference;
                            changed = true;
                            break;

                        case SerializedPropertyType.ExposedReference:
                            previewIterator.exposedReferenceValue = mirroredReference;
                            changed = true;
                            break;
                    }
                }

                sourceHasProperty = sourceIterator.Next(false);
                previewHasProperty = previewIterator.Next(false);
            }

            if (sourceHasProperty != previewHasProperty)
            {
                errorMessage = $"Mirrored serialized state diverged while rebinding '{sourceComponent.GetType().Name}'.";
                return false;
            }

            if (changed)
            {
                previewObject.ApplyModifiedPropertiesWithoutUndo();
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryGetMirroredReferenceValue (
            SerializedProperty sourceProperty,
            TemporaryMirrorMapping mirrorMapping,
            out UnityEngine.Object? mirroredReference)
        {
            mirroredReference = null;
            switch (sourceProperty.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    if (sourceProperty.objectReferenceValue == null)
                    {
                        return false;
                    }

                    return mirrorMapping.TryGetPreviewObject(sourceProperty.objectReferenceValue, out mirroredReference);

                case SerializedPropertyType.ExposedReference:
                    if (sourceProperty.exposedReferenceValue == null)
                    {
                        return false;
                    }

                    return mirrorMapping.TryGetPreviewObject(sourceProperty.exposedReferenceValue, out mirroredReference);

                default:
                    return false;
            }
        }

        private static bool ShouldSkipInternalObjectReferenceProperty (SerializedProperty property)
        {
            return string.Equals(property.propertyPath, "m_Script", StringComparison.Ordinal)
                   || string.Equals(property.propertyPath, "m_GameObject", StringComparison.Ordinal);
        }
    }
}
