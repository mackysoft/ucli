using System;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves parsed selectors either to stable references or to live Unity objects. </summary>
    internal static class ResolveReferenceResolver
    {
        /// <summary> Determines whether GlobalObjectId text is syntactically valid. </summary>
        /// <param name="globalObjectIdText"> The GlobalObjectId text value. </param>
        /// <returns> <see langword="true" /> when text is syntactically valid; otherwise <see langword="false" />. </returns>
        public static bool IsValidGlobalObjectIdText (string globalObjectIdText)
        {
            return GlobalObjectId.TryParse(globalObjectIdText, out _);
        }

        /// <summary> Tries to resolve one selector to a GlobalObjectId-normalized reference. </summary>
        /// <param name="selector"> The parsed selector. </param>
        /// <param name="executionContext"> The current request execution context. Must not be <see langword="null" />. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to observe request-local scene/prefab planning state before falling back to stable non-temporary sources. </param>
        /// <param name="resolvedReference"> The resolved reference when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryResolveStableReference (
            ResolveSelector selector,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            switch (selector.Kind)
            {
                case ResolveSelectorKind.SceneHierarchyPath:
                    return TryResolveSceneStableReference(
                        selector.ScenePath!,
                        selector.HierarchyPath!,
                        componentType: null,
                        executionContext,
                        allowTemporaryState,
                        out resolvedReference,
                        out errorMessage);

                case ResolveSelectorKind.SceneComponent:
                    return TryResolveSceneStableReference(
                        selector.ScenePath!,
                        selector.HierarchyPath!,
                        selector.ComponentType!,
                        executionContext,
                        allowTemporaryState,
                        out resolvedReference,
                        out errorMessage);

                case ResolveSelectorKind.PrefabHierarchyPath:
                    return TryResolvePrefabStableReference(
                        selector.PrefabPath!,
                        selector.HierarchyPath!,
                        componentType: null,
                        executionContext,
                        allowTemporaryState,
                        out resolvedReference,
                        out errorMessage);

                case ResolveSelectorKind.PrefabComponent:
                    return TryResolvePrefabStableReference(
                        selector.PrefabPath!,
                        selector.HierarchyPath!,
                        selector.ComponentType!,
                        executionContext,
                        allowTemporaryState,
                        out resolvedReference,
                        out errorMessage);

                default:
                    return TryResolveStableReferenceFromLiveObject(
                        selector,
                        executionContext,
                        allowTemporaryState,
                        out resolvedReference,
                        out errorMessage);
            }
        }

        /// <summary> Tries to resolve one selector to a live Unity object without requiring a stable GlobalObjectId. </summary>
        /// <param name="selector"> The parsed selector. </param>
        /// <param name="executionContext"> The current request execution context. Must not be <see langword="null" />. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to allow request-local scene or prefab planning state that was explicitly prepared earlier in the same request. </param>
        /// <param name="unityObject"> The resolved live Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryResolveUnityObject (
            ResolveSelector selector,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            return TryResolveCore(
                selector,
                executionContext,
                allowTemporaryState,
                out unityObject,
                out errorMessage);
        }

        private static bool TryResolveStableReferenceFromLiveObject (
            ResolveSelector selector,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            resolvedReference = null;
            if (!TryResolveCore(
                    selector,
                    executionContext,
                    allowTemporaryState,
                    out var unityObject,
                    out errorMessage))
            {
                return false;
            }

            return TryCreateResolvedReference(unityObject!, out resolvedReference, out errorMessage);
        }

        private static bool TryResolveSceneStableReference (
            string scenePath,
            string hierarchyPath,
            string? componentType,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            resolvedReference = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene))
            {
                if (!TryResolveSceneTargetFromScene(
                        temporaryScene,
                        hierarchyPath,
                        componentType,
                        executionContext,
                        allowTemporaryState: true,
                        out var temporaryTarget,
                        out errorMessage))
                {
                    return false;
                }

                if (TryCreateResolvedReference(temporaryTarget!, out resolvedReference, out _))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                if (executionContext.TryResolveTemporarySceneSourceObject(scenePath, temporaryTarget!, out var mirroredSourceObject)
                    && mirroredSourceObject != null
                    && TryCreateResolvedReference(mirroredSourceObject, out resolvedReference, out _))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
                return false;
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(scenePath, out _, out errorMessage))
            {
                return false;
            }

            return TryResolveStableReferenceFromLiveObject(
                ResolveSelector.FromSceneHierarchy(scenePath, hierarchyPath, componentType),
                executionContext,
                allowTemporaryState: false,
                out resolvedReference,
                out errorMessage);
        }

        private static bool TryResolvePrefabStableReference (
            string prefabPath,
            string hierarchyPath,
            string? componentType,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            resolvedReference = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabRoot)
                && temporaryPrefabRoot != null)
            {
                if (!TryResolvePrefabTargetFromRoot(
                        temporaryPrefabRoot,
                        hierarchyPath,
                        componentType,
                        executionContext,
                        allowTemporaryState: true,
                        out var temporaryTarget,
                        out errorMessage))
                {
                    return false;
                }

                if (TryCreateResolvedReference(temporaryTarget!, out resolvedReference, out _))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                if (executionContext.TryResolveTemporaryPrefabSourceObject(prefabPath, temporaryTarget!, out var mirroredSourceObject)
                    && mirroredSourceObject != null)
                {
                    if (executionContext.TryResolveTemporaryPrefabStableSourceObject(prefabPath, temporaryTarget!, out var mirroredStableSourceObject)
                        && mirroredStableSourceObject != null
                        && TryCreateResolvedReference(mirroredStableSourceObject, out resolvedReference, out _))
                    {
                        errorMessage = string.Empty;
                        return true;
                    }

                    if (TryCreateResolvedReferenceFromPrefabMirrorSource(prefabPath, mirroredSourceObject, out resolvedReference, out _))
                    {
                        errorMessage = string.Empty;
                        return true;
                    }
                }

                errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
                return false;
            }

            if (PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out var prefabStage, out _))
            {
                var prefabContentsRoot = prefabStage!.prefabContentsRoot;
                if (prefabContentsRoot == null)
                {
                    errorMessage = $"Prefab root is not available after open: {prefabPath}.";
                    return false;
                }

                if (!TryResolvePrefabTargetFromRoot(
                        prefabContentsRoot,
                        hierarchyPath,
                        componentType,
                        executionContext,
                        allowTemporaryState,
                        out var openedStageTarget,
                        out errorMessage))
                {
                    return false;
                }

                if (TryCreateResolvedReference(openedStageTarget!, out resolvedReference, out _))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
                return false;
            }

            if (!TryResolvePrefabAssetTarget(prefabPath, hierarchyPath, componentType, out var unityObject, out errorMessage))
            {
                return false;
            }

            return TryCreateResolvedReference(unityObject!, out resolvedReference, out errorMessage);
        }

        /// <summary>
        /// Resolves one selector into the shared live-object intermediate that both stable-reference and live-object callers consume.
        /// </summary>
        /// <param name="selector"> The parsed selector. </param>
        /// <param name="executionContext"> The current request execution context. Must not be <see langword="null" />. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to allow request-local scene/prefab planning state to participate in selector resolution. </param>
        /// <param name="unityObject"> The resolved live Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when selector resolution succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryResolveCore (
            ResolveSelector selector,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            switch (selector.Kind)
            {
                case ResolveSelectorKind.GlobalObjectId:
                    return TryResolveUnityObjectFromGlobalObjectId(selector.GlobalObjectId!, out unityObject, out errorMessage);

                case ResolveSelectorKind.AssetGuid:
                    return TryResolveAssetObjectFromGuid(selector.AssetGuid!, out unityObject, out errorMessage);

                case ResolveSelectorKind.AssetPath:
                    return TryResolveAssetObjectFromPath(selector.AssetPath!, out unityObject, out errorMessage);

                case ResolveSelectorKind.ProjectAssetPath:
                    return TryResolveAssetObjectFromPath(selector.ProjectAssetPath!, out unityObject, out errorMessage);

                case ResolveSelectorKind.SceneHierarchyPath:
                    return TryResolveSceneTarget(
                        selector.ScenePath!,
                        selector.HierarchyPath!,
                        componentType: null,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                case ResolveSelectorKind.SceneComponent:
                    return TryResolveSceneTarget(
                        selector.ScenePath!,
                        selector.HierarchyPath!,
                        selector.ComponentType!,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                case ResolveSelectorKind.PrefabHierarchyPath:
                    return TryResolvePrefabTarget(
                        selector.PrefabPath!,
                        selector.HierarchyPath!,
                        componentType: null,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                case ResolveSelectorKind.PrefabComponent:
                    return TryResolvePrefabTarget(
                        selector.PrefabPath!,
                        selector.HierarchyPath!,
                        selector.ComponentType!,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                default:
                    unityObject = null;
                    errorMessage = "Operation selector kind is not supported.";
                    return false;
            }
        }

        private static bool TryResolveSceneGameObject (
            string scenePath,
            string hierarchyPath,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene))
            {
                return SceneHierarchyPathResolver.TryResolveSceneObject(temporaryScene, hierarchyPath, out gameObject, out errorMessage);
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(scenePath, out var loadedScene, out errorMessage))
            {
                return false;
            }

            return SceneHierarchyPathResolver.TryResolveSceneObject(loadedScene, hierarchyPath, out gameObject, out errorMessage);
        }

        /// <summary>
        /// Resolves one scene selector to either the addressed GameObject or one unique component under that GameObject.
        /// </summary>
        /// <param name="scenePath"> The owning scene asset path. </param>
        /// <param name="hierarchyPath"> The hierarchy path inside the scene. </param>
        /// <param name="componentType"> The optional component type. <see langword="null" /> resolves the GameObject itself. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="allowTemporaryState"> Whether request-local temporary state may participate in resolution. </param>
        /// <param name="unityObject"> The resolved target when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when selector resolution succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryResolveSceneTarget (
            string scenePath,
            string hierarchyPath,
            string? componentType,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!TryResolveSceneGameObject(
                scenePath,
                hierarchyPath,
                executionContext,
                allowTemporaryState,
                out var gameObject,
                    out errorMessage))
            {
                return false;
            }

            return TryResolveTargetObjectOrComponent(
                gameObject!,
                hierarchyPath,
                componentType,
                executionContext,
                allowTemporaryState,
                out unityObject,
                out errorMessage);
        }

        /// <summary>
        /// Resolves one prefab selector to either the addressed GameObject or one unique component under that GameObject.
        /// </summary>
        /// <param name="prefabPath"> The owning prefab asset path. </param>
        /// <param name="hierarchyPath"> The hierarchy path inside the prefab. </param>
        /// <param name="componentType"> The optional component type. <see langword="null" /> resolves the GameObject itself. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="allowTemporaryState"> Whether request-local temporary state may participate in resolution. </param>
        /// <param name="unityObject"> The resolved target when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when selector resolution succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryResolvePrefabTarget (
            string prefabPath,
            string hierarchyPath,
            string? componentType,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!TryResolvePrefabGameObject(prefabPath, hierarchyPath, executionContext, allowTemporaryState, out var gameObject, out errorMessage))
            {
                return false;
            }

            return TryResolveTargetObjectOrComponent(
                gameObject!,
                hierarchyPath,
                componentType,
                executionContext,
                allowTemporaryState,
                out unityObject,
                out errorMessage);
        }

        private static bool TryResolvePrefabTargetFromRoot (
            GameObject prefabRootOrTarget,
            string hierarchyPath,
            string? componentType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            GameObject? gameObject;
            if (string.IsNullOrEmpty(hierarchyPath))
            {
                gameObject = prefabRootOrTarget;
                errorMessage = string.Empty;
            }
            else if (!PrefabHierarchyPathResolver.TryResolve(prefabRootOrTarget, hierarchyPath, out gameObject, out errorMessage))
            {
                return false;
            }

            if (componentType == null)
            {
                unityObject = gameObject;
                errorMessage = string.Empty;
                return true;
            }

            if (!ComponentOperationUtilities.TryResolveComponentSelector(
                    gameObject!,
                    componentType,
                    executionContext,
                    allowTemporaryState,
                    out var resolution,
                    out errorMessage))
            {
                return false;
            }

            if (resolution.MatchCount == 0)
            {
                unityObject = null;
                errorMessage = $"Component type '{componentType}' was not found at '{hierarchyPath}'.";
                return false;
            }

            if (resolution.MatchCount > 1)
            {
                unityObject = null;
                errorMessage = $"Component type '{componentType}' resolved multiple components at '{hierarchyPath}'.";
                return false;
            }

            unityObject = resolution.Component;
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveSceneTargetFromScene (
            UnityEngine.SceneManagement.Scene scene,
            string hierarchyPath,
            string? componentType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!SceneHierarchyPathResolver.TryResolveSceneObject(scene, hierarchyPath, out var gameObject, out errorMessage))
            {
                return false;
            }

            return TryResolveTargetObjectOrComponent(
                gameObject!,
                hierarchyPath,
                componentType,
                executionContext,
                allowTemporaryState,
                out unityObject,
                out errorMessage);
        }

        private static bool TryResolvePrefabAssetTarget (
            string prefabPath,
            string hierarchyPath,
            string? componentType,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!TryResolvePrefabAssetRoot(prefabPath, out var prefabAssetRoot, out errorMessage))
            {
                return false;
            }

            return TryResolvePrefabTargetFromRoot(
                prefabAssetRoot!,
                hierarchyPath,
                componentType,
                executionContext: null,
                allowTemporaryState: false,
                out unityObject,
                out errorMessage);
        }

        private static bool TryResolveOpenedPrefabStageTarget (
            string prefabPath,
            string hierarchyPath,
            string? componentType,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out var prefabStage, out _))
            {
                errorMessage = string.Empty;
                return false;
            }

            var prefabRoot = prefabStage!.prefabContentsRoot;
            if (prefabRoot == null)
            {
                errorMessage = $"Prefab root is not available after open: {prefabPath}.";
                return false;
            }

            return TryResolvePrefabTargetFromRoot(
                prefabRoot,
                hierarchyPath,
                componentType,
                executionContext: null,
                allowTemporaryState: false,
                out unityObject,
                out errorMessage);
        }

        private static bool TryResolvePrefabGameObject (
            string prefabPath,
            string hierarchyPath,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabRoot)
                && temporaryPrefabRoot != null)
            {
                return PrefabHierarchyPathResolver.TryResolve(temporaryPrefabRoot, hierarchyPath, out gameObject, out errorMessage);
            }

            if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out var prefabStage, out _))
            {
                errorMessage = $"Prefab is not opened: {prefabPath}. Use 'ucli.prefab.open' first.";
                return false;
            }

            var prefabContentsRoot = prefabStage!.prefabContentsRoot;
            if (prefabContentsRoot == null)
            {
                errorMessage = $"Prefab root is not available after open: {prefabPath}.";
                return false;
            }

            return PrefabHierarchyPathResolver.TryResolve(prefabContentsRoot, hierarchyPath, out gameObject, out errorMessage);
        }

        private static bool TryResolvePrefabAssetRoot (
            string prefabPath,
            out GameObject? prefabAssetRoot,
            out string errorMessage)
        {
            prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAssetRoot == null)
            {
                errorMessage = $"Prefab path could not be resolved to a prefab asset: {prefabPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Resolves one GlobalObjectId literal to the live Unity object that currently exposes that identity.
        /// </summary>
        /// <param name="globalObjectIdText"> The GlobalObjectId text literal. </param>
        /// <param name="unityObject"> The resolved Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when the GlobalObjectId resolves in the current editor state; otherwise <see langword="false" />. </returns>
        private static bool TryResolveUnityObjectFromGlobalObjectId (
            string globalObjectIdText,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (!GlobalObjectId.TryParse(globalObjectIdText, out var globalObjectId))
            {
                unityObject = null;
                errorMessage = $"'{ResolveSelectorPropertyNames.GlobalObjectId}' must be a valid GlobalObjectId string.";
                return false;
            }

            unityObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            if (unityObject == null)
            {
                errorMessage = $"GlobalObjectId is not resolvable in current project state: {globalObjectIdText}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveAssetObjectFromGuid (
            string assetGuid,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                errorMessage = $"Asset GUID could not be resolved: {assetGuid}.";
                return false;
            }

            return TryResolveAssetObjectFromPath(assetPath, out unityObject, out errorMessage);
        }

        private static bool TryResolveAssetObjectFromPath (
            string assetPath,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (unityObject == null)
            {
                errorMessage = $"Asset path could not be resolved to a main asset: {assetPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveTargetObjectOrComponent (
            GameObject gameObject,
            string hierarchyPath,
            string? componentType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (componentType == null)
            {
                unityObject = gameObject;
                errorMessage = string.Empty;
                return true;
            }

            if (!ComponentOperationUtilities.TryResolveComponentSelector(
                    gameObject,
                    componentType,
                    executionContext,
                    allowTemporaryState,
                    out var resolution,
                    out errorMessage))
            {
                return false;
            }

            if (resolution.MatchCount == 0)
            {
                unityObject = null;
                errorMessage = $"Component type '{componentType}' was not found at '{hierarchyPath}'.";
                return false;
            }

            if (resolution.MatchCount > 1)
            {
                unityObject = null;
                errorMessage = $"Component type '{componentType}' resolved multiple components at '{hierarchyPath}'.";
                return false;
            }

            unityObject = resolution.Component;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Converts one Unity object into a GlobalObjectId-normalized reference when the object exposes stable editor identity. </summary>
        /// <param name="unityObject"> The Unity object to normalize. </param>
        /// <param name="resolvedReference"> The normalized resolved reference when successful. </param>
        /// <param name="errorMessage"> The validation error message when the object has no stable editor identity. </param>
        /// <returns> <see langword="true" /> when a stable resolved reference can be created; otherwise <see langword="false" />. </returns>
        private static bool TryCreateResolvedReference (
            UnityEngine.Object unityObject,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            if (!UnityObjectReferenceResolver.TryCreateResolvedReference(unityObject, out resolvedReference))
            {
                errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryCreateResolvedReferenceFromPrefabMirrorSource (
            string prefabPath,
            UnityEngine.Object unityObject,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            if (TryCreateResolvedReference(unityObject, out resolvedReference, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            var prefabSourceAtPath = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(unityObject, prefabPath);
            if (prefabSourceAtPath != null
                && TryCreateResolvedReference(prefabSourceAtPath, out resolvedReference, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            var prefabSourceObject = PrefabUtility.GetCorrespondingObjectFromSource(unityObject);
            if (prefabSourceObject != null
                && TryCreateResolvedReference(prefabSourceObject, out resolvedReference, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            var originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(unityObject);
            if (originalSource != null
                && TryCreateResolvedReference(originalSource, out resolvedReference, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
            return false;
        }
    }
}
