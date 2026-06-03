using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        /// <summary> Registers explicit stable references for one mirrored live hierarchy. </summary>
        /// <param name="sourceRoot"> The live hierarchy root that already matches the preview hierarchy shape. </param>
        /// <param name="previewRoot"> The preview hierarchy root. </param>
        /// <param name="stableReferenceIndex"> The destination stable-reference index. </param>
        public static void RegisterMirroredHierarchyStableReferencesBestEffort (
            GameObject sourceRoot,
            GameObject previewRoot,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            if (sourceRoot == null)
            {
                throw new ArgumentNullException(nameof(sourceRoot));
            }

            if (previewRoot == null)
            {
                throw new ArgumentNullException(nameof(previewRoot));
            }

            if (stableReferenceIndex == null)
            {
                throw new ArgumentNullException(nameof(stableReferenceIndex));
            }

            TryRegisterStableReferenceFromSource(previewRoot, sourceRoot, stableReferenceIndex);

            var sourceComponents = sourceRoot.GetComponents<Component>();
            var previewComponents = previewRoot.GetComponents<Component>();
            var componentCount = Math.Min(sourceComponents.Length, previewComponents.Length);
            for (var componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                var sourceComponent = sourceComponents[componentIndex];
                var previewComponent = previewComponents[componentIndex];
                if (sourceComponent == null || previewComponent == null)
                {
                    continue;
                }

                if (sourceComponent.GetType() != previewComponent.GetType())
                {
                    continue;
                }

                TryRegisterStableReferenceFromSource(previewComponent, sourceComponent, stableReferenceIndex);
            }

            var childCount = Math.Min(sourceRoot.transform.childCount, previewRoot.transform.childCount);
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                RegisterMirroredHierarchyStableReferencesBestEffort(
                    sourceRoot.transform.GetChild(childIndex).gameObject,
                    previewRoot.transform.GetChild(childIndex).gameObject,
                    stableReferenceIndex);
            }
        }

        /// <summary> Registers stable GlobalObjectId entries for one preview scene opened from persisted contents. </summary>
        /// <param name="previewScene"> The preview scene. </param>
        /// <param name="mirrorMapping"> The mapping that receives stable reference entries. </param>
        public static void RegisterPreviewSceneStableReferencesBestEffort (
            Scene previewScene,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            if (stableReferenceIndex == null)
            {
                throw new ArgumentNullException(nameof(stableReferenceIndex));
            }

            if (!previewScene.IsValid() || !previewScene.isLoaded)
            {
                return;
            }

            var roots = previewScene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                RegisterPreviewSceneHierarchyStableReferencesBestEffort(roots[i], stableReferenceIndex);
            }
        }

        /// <summary> Registers stable GlobalObjectId entries for one temporary prefab contents hierarchy. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="prefabContentsRoot"> The temporary prefab contents root. </param>
        /// <param name="mirrorMapping"> The mapping that receives stable reference entries. </param>
        public static void RegisterPreviewPrefabStableReferencesBestEffort (
            string prefabPath,
            GameObject prefabContentsRoot,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            if (prefabContentsRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabContentsRoot));
            }

            if (stableReferenceIndex == null)
            {
                throw new ArgumentNullException(nameof(stableReferenceIndex));
            }

            RegisterPreviewPrefabHierarchyStableReferencesBestEffort(prefabPath, prefabContentsRoot, stableReferenceIndex);
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

        private static void RegisterPreviewSceneHierarchyStableReferencesBestEffort (
            GameObject previewRoot,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            TryRegisterStableReference(previewRoot, stableReferenceIndex);

            var components = previewRoot.GetComponents<Component>();
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                if (component == null)
                {
                    continue;
                }

                TryRegisterStableReference(component, stableReferenceIndex);
            }

            for (var childIndex = 0; childIndex < previewRoot.transform.childCount; childIndex++)
            {
                RegisterPreviewSceneHierarchyStableReferencesBestEffort(
                    previewRoot.transform.GetChild(childIndex).gameObject,
                    stableReferenceIndex);
            }
        }

        private static void RegisterPreviewPrefabHierarchyStableReferencesBestEffort (
            string prefabPath,
            GameObject previewRoot,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            TryRegisterPrefabStableReference(previewRoot, prefabPath, stableReferenceIndex);

            var components = previewRoot.GetComponents<Component>();
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                if (component == null)
                {
                    continue;
                }

                TryRegisterPrefabStableReference(component, prefabPath, stableReferenceIndex);
            }

            for (var childIndex = 0; childIndex < previewRoot.transform.childCount; childIndex++)
            {
                RegisterPreviewPrefabHierarchyStableReferencesBestEffort(
                    prefabPath,
                    previewRoot.transform.GetChild(childIndex).gameObject,
                    stableReferenceIndex);
            }
        }

        private static void TryRegisterStableReference (
            UnityEngine.Object previewObject,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(previewObject, out var resolvedReference))
            {
                stableReferenceIndex.Add(previewObject, resolvedReference!.GlobalObjectId);
            }
        }

        private static void TryRegisterStableReferenceFromSource (
            UnityEngine.Object previewObject,
            UnityEngine.Object sourceObject,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(sourceObject, out var resolvedReference))
            {
                stableReferenceIndex.Add(previewObject, resolvedReference!.GlobalObjectId);
            }
        }

        private static void TryRegisterPrefabStableReference (
            UnityEngine.Object previewObject,
            string prefabPath,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(previewObject, out var resolvedReference))
            {
                stableReferenceIndex.Add(previewObject, resolvedReference!.GlobalObjectId);
                return;
            }

            var stableSourceObject = TryGetPrefabStableSourceObject(previewObject, prefabPath);
            if (stableSourceObject != null
                && UnityObjectReferenceResolver.TryCreateResolvedReference(stableSourceObject, out resolvedReference))
            {
                stableReferenceIndex.Add(previewObject, resolvedReference!.GlobalObjectId);
            }
        }

        private static UnityEngine.Object? TryGetPrefabStableSourceObject (
            UnityEngine.Object unityObject,
            string prefabPath)
        {
            if (!string.IsNullOrWhiteSpace(prefabPath))
            {
                var sourceAtPath = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(unityObject, prefabPath);
                if (sourceAtPath != null)
                {
                    return sourceAtPath;
                }
            }

            var sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(unityObject);
            if (sourceObject != null)
            {
                return sourceObject;
            }

            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(unityObject);
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
