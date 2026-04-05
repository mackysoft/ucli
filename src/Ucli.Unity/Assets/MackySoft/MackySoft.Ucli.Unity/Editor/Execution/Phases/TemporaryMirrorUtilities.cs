using System;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides shared helpers for building and repairing one mirrored preview object graph from a live source graph. </summary>
    internal static class TemporaryMirrorUtilities
    {
        /// <summary> Registers one mirrored live hierarchy into the supplied mirror mapping. </summary>
        /// <param name="sourceRoot"> The live hierarchy root to mirror. Must not be <see langword="null" />. </param>
        /// <param name="previewRoot"> The request-local preview root cloned from <paramref name="sourceRoot" />. Must not be <see langword="null" />. </param>
        /// <param name="mirrorMapping"> The mirror mapping that records object and component pairs. Must not be <see langword="null" />. </param>
        /// <param name="errorMessage"> The validation error message when the preview hierarchy no longer matches the live hierarchy shape. </param>
        /// <returns> <see langword="true" /> when the mirrored hierarchy can be registered without shape divergence; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="sourceRoot" />, <paramref name="previewRoot" />, or <paramref name="mirrorMapping" /> is <see langword="null" />. </exception>
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

        /// <summary> Rebinds scene-local or prefab-local object references inside one mirrored preview graph. </summary>
        /// <param name="mirrorMapping"> The mirror mapping that relates live objects to preview objects. Must not be <see langword="null" />. </param>
        /// <param name="errorMessage"> The validation error message when the mirrored serialized layouts diverge during rebinding. </param>
        /// <returns> <see langword="true" /> when all mirrored object references can be redirected to preview objects; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="mirrorMapping" /> is <see langword="null" />. </exception>
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

        /// <summary> Registers best-effort stable-source pairs for one mirrored prefab hierarchy. </summary>
        /// <param name="sourceRoot"> The mirrored live prefab root. Must not be <see langword="null" />. </param>
        /// <param name="stableRoot"> The persisted prefab root used as the stable-source baseline. Must not be <see langword="null" />. </param>
        /// <param name="mirrorMapping"> The mirror mapping that receives stable-source pairs. Must not be <see langword="null" />. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="sourceRoot" />, <paramref name="stableRoot" />, or <paramref name="mirrorMapping" /> is <see langword="null" />. </exception>
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

                RegisterStableSourceHierarchyBestEffort(sourceChild, stableChildren[stableMatchIndex], mirrorMapping);
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
