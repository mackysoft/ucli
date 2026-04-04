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
            RegisterStableSourceComponentsBestEffort(sourceRoot, stableRoot, mirrorMapping);
            RegisterStableSourceChildrenBestEffort(sourceRoot.transform, stableRoot.transform, mirrorMapping);
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

        private static void RegisterStableSourceComponentsBestEffort (
            GameObject sourceRoot,
            GameObject stableRoot,
            TemporaryMirrorMapping mirrorMapping)
        {
            var sourceComponents = sourceRoot.GetComponents<Component>();
            var stableComponents = stableRoot.GetComponents<Component>();
            if (HaveMatchingComponentTypeSequence(sourceComponents, stableComponents))
            {
                for (var i = 0; i < sourceComponents.Length; i++)
                {
                    var sourceComponent = sourceComponents[i];
                    var stableComponent = stableComponents[i];
                    if (sourceComponent == null || stableComponent == null)
                    {
                        continue;
                    }

                    mirrorMapping.AddStableSourcePair(sourceComponent, stableComponent);
                }

                return;
            }

            for (var sourceIndex = 0; sourceIndex < sourceComponents.Length; sourceIndex++)
            {
                var sourceComponent = sourceComponents[sourceIndex];
                if (sourceComponent == null)
                {
                    continue;
                }

                var sourceType = sourceComponent.GetType();
                var sourceMatchCount = 0;
                for (var i = 0; i < sourceComponents.Length; i++)
                {
                    if (sourceComponents[i] != null && sourceComponents[i]!.GetType() == sourceType)
                    {
                        sourceMatchCount++;
                    }
                }

                if (sourceMatchCount != 1)
                {
                    continue;
                }

                var stableMatchIndex = -1;
                var stableMatchCount = 0;
                for (var stableIndex = 0; stableIndex < stableComponents.Length; stableIndex++)
                {
                    var stableComponent = stableComponents[stableIndex];
                    if (stableComponent != null && stableComponent.GetType() == sourceType)
                    {
                        stableMatchIndex = stableIndex;
                        stableMatchCount++;
                    }
                }

                if (stableMatchCount != 1)
                {
                    continue;
                }

                var stableMatchedComponent = stableComponents[stableMatchIndex];
                if (stableMatchedComponent != null)
                {
                    mirrorMapping.AddStableSourcePair(sourceComponent, stableMatchedComponent);
                }
            }
        }

        private static void RegisterStableSourceChildrenBestEffort (
            Transform sourceTransform,
            Transform stableTransform,
            TemporaryMirrorMapping mirrorMapping)
        {
            var sourceChildren = GetDirectChildGameObjects(sourceTransform);
            var stableChildren = GetDirectChildGameObjects(stableTransform);
            var matchedSource = new bool[sourceChildren.Length];
            var matchedStable = new bool[stableChildren.Length];

            for (var sourceIndex = 0; sourceIndex < sourceChildren.Length; sourceIndex++)
            {
                var sourceChild = sourceChildren[sourceIndex];
                var sourceNameMatchCount = CountChildrenWithName(sourceChildren, sourceChild.name);
                if (sourceNameMatchCount != 1)
                {
                    continue;
                }

                var stableMatchIndex = FindUniqueChildIndexByName(stableChildren, sourceChild.name, out var stableNameMatchCount);
                if (stableNameMatchCount != 1)
                {
                    continue;
                }

                matchedSource[sourceIndex] = true;
                matchedStable[stableMatchIndex] = true;
                RegisterStableSourceHierarchyBestEffort(sourceChild, stableChildren[stableMatchIndex], mirrorMapping);
            }

            var remainingSourceCount = 0;
            for (var i = 0; i < matchedSource.Length; i++)
            {
                if (!matchedSource[i])
                {
                    remainingSourceCount++;
                }
            }

            var remainingStableCount = 0;
            for (var i = 0; i < matchedStable.Length; i++)
            {
                if (!matchedStable[i])
                {
                    remainingStableCount++;
                }
            }

            if (remainingSourceCount != remainingStableCount)
            {
                return;
            }

            var stableCursor = 0;
            for (var sourceIndex = 0; sourceIndex < sourceChildren.Length; sourceIndex++)
            {
                if (matchedSource[sourceIndex])
                {
                    continue;
                }

                while (stableCursor < stableChildren.Length && matchedStable[stableCursor])
                {
                    stableCursor++;
                }

                if (stableCursor >= stableChildren.Length)
                {
                    return;
                }

                var sourceChild = sourceChildren[sourceIndex];
                var stableChild = stableChildren[stableCursor];
                if (!HaveCompatibleHierarchyShape(sourceChild, stableChild))
                {
                    return;
                }

                RegisterStableSourceHierarchyBestEffort(sourceChild, stableChild, mirrorMapping);
                matchedSource[sourceIndex] = true;
                matchedStable[stableCursor] = true;
                stableCursor++;
            }
        }

        private static GameObject[] GetDirectChildGameObjects (Transform transform)
        {
            var childCount = transform.childCount;
            var children = new GameObject[childCount];
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                children[childIndex] = transform.GetChild(childIndex).gameObject;
            }

            return children;
        }

        private static int CountChildrenWithName (
            GameObject[] children,
            string childName)
        {
            var count = 0;
            for (var childIndex = 0; childIndex < children.Length; childIndex++)
            {
                if (string.Equals(children[childIndex].name, childName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static int FindUniqueChildIndexByName (
            GameObject[] children,
            string childName,
            out int matchCount)
        {
            var matchedIndex = -1;
            matchCount = 0;
            for (var childIndex = 0; childIndex < children.Length; childIndex++)
            {
                if (!string.Equals(children[childIndex].name, childName, StringComparison.Ordinal))
                {
                    continue;
                }

                matchedIndex = childIndex;
                matchCount++;
            }

            return matchedIndex;
        }

        private static bool HaveCompatibleHierarchyShape (
            GameObject sourceObject,
            GameObject stableObject)
        {
            if (sourceObject.transform.childCount != stableObject.transform.childCount)
            {
                return false;
            }

            return HaveMatchingComponentTypeSequence(
                sourceObject.GetComponents<Component>(),
                stableObject.GetComponents<Component>());
        }

        private static bool HaveMatchingComponentTypeSequence (
            Component[] sourceComponents,
            Component[] stableComponents)
        {
            if (sourceComponents.Length != stableComponents.Length)
            {
                return false;
            }

            for (var i = 0; i < sourceComponents.Length; i++)
            {
                var sourceComponent = sourceComponents[i];
                var stableComponent = stableComponents[i];
                if (sourceComponent == null || stableComponent == null)
                {
                    if (sourceComponent != stableComponent)
                    {
                        return false;
                    }

                    continue;
                }

                if (sourceComponent.GetType() != stableComponent.GetType())
                {
                    return false;
                }
            }

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
