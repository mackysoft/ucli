using System;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Unity.SceneInspection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves parsed selectors either to stable references or to live Unity objects. </summary>
    internal static class ResolveReferenceResolver
    {
        /// <summary> Tries to create one stable reference for a GameObject, including request-local preview fallback for mirrored scene and prefab plan state. </summary>
        /// <param name="gameObject"> The GameObject whose stable identity is required. </param>
        /// <param name="executionContext"> The current request execution context when request-local preview fallback should be considered. </param>
        /// <param name="stableObjectId"> The resolved stable reference when successful. </param>
        /// <returns> <see langword="true" /> when a stable reference can be created; otherwise <see langword="false" />. </returns>
        public static bool TryCreateGameObjectGlobalObjectId (
            GameObject gameObject,
            OperationExecutionContext? executionContext,
            [NotNullWhen(true)] out UnityGlobalObjectId? stableObjectId)
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            stableObjectId = null;
            if (TryCreateGlobalObjectId(gameObject, out stableObjectId, out _))
            {
                return true;
            }

            if (executionContext == null)
            {
                return false;
            }

            if (TryCreateGlobalObjectIdFromPreviewSceneObject(gameObject, executionContext, out stableObjectId))
            {
                return true;
            }

            return TryCreateGlobalObjectIdFromPreviewPrefabObject(gameObject, executionContext, out stableObjectId);
        }

        /// <summary> Tries to resolve one selector to a GlobalObjectId-normalized reference. </summary>
        /// <param name="selector"> The parsed selector. </param>
        /// <param name="executionContext"> The current request execution context. Must not be <see langword="null" />. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to observe request-local scene/prefab planning state before falling back to stable non-temporary sources. </param>
        /// <param name="stableObjectId"> The resolved reference when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryResolveStableReference (
            ResolveSelector selector,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            [NotNullWhen(true)] out UnityGlobalObjectId? stableObjectId,
            out string errorMessage)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            switch (selector.Kind)
            {
                case ResolveSelectorKind.GlobalObjectId:
                    return TryResolveGlobalObjectIdStableReference(
                        selector.GlobalObjectId!,
                        executionContext,
                        allowTemporaryState,
                        out stableObjectId,
                        out errorMessage);

                case ResolveSelectorKind.SceneHierarchyPath:
                    return TryResolveSceneStableReference(
                        selector.ScenePath!,
                        selector.HierarchyPath!,
                        componentType: null,
                        executionContext,
                        allowTemporaryState,
                        out stableObjectId,
                        out errorMessage);

                case ResolveSelectorKind.SceneComponent:
                    return TryResolveSceneStableReference(
                        selector.ScenePath!,
                        selector.HierarchyPath!,
                        selector.ComponentType!,
                        executionContext,
                        allowTemporaryState,
                        out stableObjectId,
                        out errorMessage);

                case ResolveSelectorKind.PrefabHierarchyPath:
                    return TryResolvePrefabStableReference(
                        selector.PrefabPath!,
                        selector.HierarchyPath!,
                        componentType: null,
                        executionContext,
                        allowTemporaryState,
                        out stableObjectId,
                        out errorMessage);

                case ResolveSelectorKind.PrefabComponent:
                    return TryResolvePrefabStableReference(
                        selector.PrefabPath!,
                        selector.HierarchyPath!,
                        selector.ComponentType!,
                        executionContext,
                        allowTemporaryState,
                        out stableObjectId,
                        out errorMessage);

                default:
                    return TryResolveStableReferenceFromLiveObject(
                        selector,
                        executionContext,
                        allowTemporaryState,
                        out stableObjectId,
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
            [NotNullWhen(true)] out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

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
            out UnityGlobalObjectId? stableObjectId,
            out string errorMessage)
        {
            stableObjectId = null;
            if (!TryResolveCore(
                    selector,
                    executionContext,
                    allowTemporaryState,
                    out var unityObject,
                    out errorMessage))
            {
                return false;
            }

            return TryCreateGlobalObjectId(unityObject!, out stableObjectId, out errorMessage);
        }

        private static bool TryResolveSceneStableReference (
            SceneAssetPath scenePath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityGlobalObjectId? stableObjectId,
            out string errorMessage)
        {
            stableObjectId = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryScene(scenePath.Value, out var temporaryScene))
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

                if (TryCreateGlobalObjectIdFromTemporarySceneTarget(
                        scenePath.Value,
                        temporaryTarget!,
                        executionContext,
                        out stableObjectId))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
                return false;
            }

            if (!SceneAssetSourceUtilities.TryGetLoadedScene(scenePath.Value, out _, out errorMessage))
            {
                return false;
            }

            return TryResolveStableReferenceFromLiveObject(
                ResolveSelector.FromSceneHierarchy(scenePath, hierarchyPath, componentType),
                executionContext,
                allowTemporaryState: false,
                out stableObjectId,
                out errorMessage);
        }

        private static bool TryResolvePrefabStableReference (
            PrefabAssetPath prefabPath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityGlobalObjectId? stableObjectId,
            out string errorMessage)
        {
            stableObjectId = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath.Value, out var temporaryPrefabRoot)
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

                if (TryCreateGlobalObjectIdFromTemporaryPrefabTarget(
                        prefabPath.Value,
                        temporaryTarget!,
                        executionContext,
                        out stableObjectId))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
                return false;
            }

            if (PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath.Value, out var prefabStage, out _))
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

                if (TryCreateGlobalObjectId(openedStageTarget!, out stableObjectId, out _))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                if (TryCreateGlobalObjectIdFromPrefabMirrorSource(prefabPath.Value, openedStageTarget!, out stableObjectId, out _))
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

            return TryCreateGlobalObjectId(unityObject!, out stableObjectId, out errorMessage);
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
                    return TryResolveUnityObjectFromGlobalObjectId(
                        selector.GlobalObjectId!,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                case ResolveSelectorKind.AssetGuid:
                    return TryResolveAssetObjectFromGuid(selector.AssetGuid!.Value, out unityObject, out errorMessage);

                case ResolveSelectorKind.AssetPath:
                    return TryResolveAssetObjectFromPath(
                        selector.AssetPath!.Value,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                case ResolveSelectorKind.ProjectAssetPath:
                    return TryResolveAssetObjectFromPath(
                        selector.ProjectAssetPath!.Value,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

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
            SceneAssetPath scenePath,
            UnityHierarchyPath hierarchyPath,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryScene(scenePath.Value, out var temporaryScene))
            {
                return SceneHierarchyPathResolver.TryResolveSceneObject(temporaryScene, hierarchyPath.Value, out gameObject, out errorMessage);
            }

            if (!SceneAssetSourceUtilities.TryGetLoadedScene(scenePath.Value, out var loadedScene, out errorMessage))
            {
                return false;
            }

            return SceneHierarchyPathResolver.TryResolveSceneObject(loadedScene, hierarchyPath.Value, out gameObject, out errorMessage);
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
            SceneAssetPath scenePath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
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
            PrefabAssetPath prefabPath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
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
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!PrefabHierarchyPathResolver.TryResolve(
                    prefabRootOrTarget,
                    hierarchyPath.Value,
                    out var gameObject,
                    out errorMessage))
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
                    componentType.Value,
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
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!SceneHierarchyPathResolver.TryResolveSceneObject(scene, hierarchyPath.Value, out var gameObject, out errorMessage))
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
            PrefabAssetPath prefabPath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
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
            PrefabAssetPath prefabPath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath.Value, out var prefabStage, out _))
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
            PrefabAssetPath prefabPath,
            UnityHierarchyPath hierarchyPath,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath.Value, out var temporaryPrefabRoot)
                && temporaryPrefabRoot != null)
            {
                return PrefabHierarchyPathResolver.TryResolve(temporaryPrefabRoot, hierarchyPath.Value, out gameObject, out errorMessage);
            }

            if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath.Value, out var prefabStage, out _))
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

            return PrefabHierarchyPathResolver.TryResolve(prefabContentsRoot, hierarchyPath.Value, out gameObject, out errorMessage);
        }

        private static bool TryResolvePrefabAssetRoot (
            PrefabAssetPath prefabPath,
            out GameObject? prefabAssetRoot,
            out string errorMessage)
        {
            prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath.Value);
            if (prefabAssetRoot == null)
            {
                errorMessage = $"Prefab path could not be resolved to a prefab asset: {prefabPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Validates one GlobalObjectId selector against current request-local state and preserves its stable identity. </summary>
        /// <param name="globalObjectId"> The parsed GlobalObjectId selector. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to honor request-local shadows, mirrors, and deletions before current live state. </param>
        /// <param name="stableObjectId"> The normalized resolved reference when the identifier is valid in current request-local state. </param>
        /// <param name="errorMessage"> The resolution error message when the identifier cannot be resolved in current request-local state. </param>
        /// <returns> <see langword="true" /> when the GlobalObjectId remains valid for the current request state; otherwise <see langword="false" />. </returns>
        private static bool TryResolveGlobalObjectIdStableReference (
            UnityGlobalObjectId globalObjectId,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityGlobalObjectId? stableObjectId,
            out string errorMessage)
        {
            stableObjectId = null;
            if (!TryResolveUnityObjectFromGlobalObjectId(
                    globalObjectId,
                    executionContext,
                    allowTemporaryState,
                    out _,
                    out errorMessage))
            {
                return false;
            }

            stableObjectId = globalObjectId;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Resolves one parsed GlobalObjectId to the live Unity object that currently exposes that identity.
        /// </summary>
        /// <param name="globalObjectId"> The parsed GlobalObjectId. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to let request-local shadows, mirrors, and deletions override current live editor state. </param>
        /// <param name="unityObject"> The resolved Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when the GlobalObjectId resolves in the current editor state; otherwise <see langword="false" />. </returns>
        private static bool TryResolveUnityObjectFromGlobalObjectId (
            UnityGlobalObjectId globalObjectId,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (globalObjectId == null)
            {
                throw new ArgumentNullException(nameof(globalObjectId));
            }

            if (!GlobalObjectId.TryParse(globalObjectId.Value, out var nativeGlobalObjectId))
            {
                unityObject = null;
                errorMessage = $"GlobalObjectId is not supported by the current Unity Editor: {globalObjectId.Value}.";
                return false;
            }

            if (allowTemporaryState)
            {
                if (executionContext.IsDeletedStableObject(globalObjectId))
                {
                    unityObject = null;
                    errorMessage = $"GlobalObjectId is not resolvable in current request-local state: {globalObjectId.Value}.";
                    return false;
                }

                var sourceIdentity = RequestLocalObjectIdentity.FromGlobalObjectId(globalObjectId);
                if (executionContext.TryGetComponentShadowState(sourceIdentity, out var componentShadowState))
                {
                    unityObject = componentShadowState.Component;
                    errorMessage = string.Empty;
                    return true;
                }

                if (executionContext.TryGetAssetShadow(globalObjectId, out unityObject, out _)
                    && unityObject != null)
                {
                    errorMessage = string.Empty;
                    return true;
                }

                if (executionContext.TryResolveTemporaryPreviewObjectFromGlobalObjectId(globalObjectId, out unityObject)
                    && unityObject != null)
                {
                    errorMessage = string.Empty;
                    return true;
                }
            }

            unityObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(nativeGlobalObjectId);
            if (unityObject == null)
            {
                errorMessage = $"GlobalObjectId is not resolvable in current project state: {globalObjectId.Value}.";
                return false;
            }

            if (allowTemporaryState
                && TryResolveRequestLocalPreviewOverride(unityObject, executionContext, out var requestLocalObject))
            {
                unityObject = requestLocalObject;
            }
            else if (allowTemporaryState
                && TryGetPrefabAssetPath(unityObject, out var prefabPath)
                && executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabRoot)
                && temporaryPrefabRoot != null)
            {
                unityObject = null;
                errorMessage = $"GlobalObjectId is not resolvable in current request-local state: {globalObjectId.Value}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to replace one live or persisted object with a request-local preview override. </summary>
        /// <param name="liveOrPersistentObject"> The live or persisted Unity object. Must not be <see langword="null" />. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="previewObject"> The request-local preview override when found. </param>
        /// <returns> <see langword="true" /> when request-local mirror state should override the supplied object; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="liveOrPersistentObject" /> is <see langword="null" />. </exception>
        private static bool TryResolveRequestLocalPreviewOverride (
            UnityEngine.Object liveOrPersistentObject,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? previewObject)
        {
            previewObject = null;
            if (liveOrPersistentObject == null)
            {
                throw new ArgumentNullException(nameof(liveOrPersistentObject));
            }

            if (TryResolvePreviewSceneObjectOverride(liveOrPersistentObject, executionContext, out previewObject))
            {
                return true;
            }

            return TryResolvePreviewPrefabObjectOverride(liveOrPersistentObject, executionContext, out previewObject);
        }

        /// <summary> Tries to replace one live scene object with its request-local preview counterpart. </summary>
        /// <param name="liveObject"> The live scene object or component. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="previewObject"> The preview counterpart when found. </param>
        /// <returns> <see langword="true" /> when the object belongs to a mirrored dirty loaded scene tracked in the request; otherwise <see langword="false" />. </returns>
        private static bool TryResolvePreviewSceneObjectOverride (
            UnityEngine.Object liveObject,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? previewObject)
        {
            previewObject = null;
            var sourceGameObject = liveObject as GameObject;
            if (sourceGameObject == null)
            {
                var sourceComponent = liveObject as Component;
                if (sourceComponent == null)
                {
                    return false;
                }

                sourceGameObject = sourceComponent.gameObject;
            }

            var scene = sourceGameObject.scene;
            if (!scene.IsValid()
                || !scene.isLoaded
                || string.IsNullOrWhiteSpace(scene.path))
            {
                return false;
            }

            if (!executionContext.TryResolveTemporaryScenePreviewObject(scene.path, liveObject, out previewObject))
            {
                if (!UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(liveObject, out var globalObjectId)
                    || !executionContext.TryResolveTemporaryPreviewObjectFromGlobalObjectId(globalObjectId, out previewObject))
                {
                    previewObject = null;
                    return false;
                }
            }

            if (previewObject == null)
            {
                previewObject = null;
                return false;
            }

            return true;
        }

        /// <summary> Tries to replace one live or persisted prefab object with its request-local preview counterpart. </summary>
        /// <param name="liveOrPersistentObject"> The live or persisted prefab object. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="previewObject"> The preview counterpart when found. </param>
        /// <returns> <see langword="true" /> when the object maps into tracked request-local prefab mirror state; otherwise <see langword="false" />. </returns>
        private static bool TryResolvePreviewPrefabObjectOverride (
            UnityEngine.Object liveOrPersistentObject,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? previewObject)
        {
            previewObject = null;
            if (TryResolvePreviewPrefabMirrorSourceObjectOverride(liveOrPersistentObject, executionContext, out previewObject))
            {
                return true;
            }

            if (!TryGetPrefabAssetPath(liveOrPersistentObject, out var prefabPath))
            {
                previewObject = null;
                return false;
            }

            if (!executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabRoot)
                || temporaryPrefabRoot == null)
            {
                previewObject = null;
                return false;
            }

            if (!UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(liveOrPersistentObject, out var globalObjectId)
                || !executionContext.TryResolveTemporaryPreviewObjectFromGlobalObjectId(globalObjectId, out previewObject))
            {
                previewObject = null;
                return false;
            }

            return previewObject != null;
        }

        /// <summary> Tries to resolve one mirrored opened-stage prefab object to its request-local preview counterpart. </summary>
        /// <param name="sourceObject"> The mirrored opened-stage prefab object. </param>
        /// <param name="executionContext"> The current request execution context. </param>
        /// <param name="previewObject"> The preview counterpart when found. </param>
        /// <returns> <see langword="true" /> when the object belongs to tracked mirrored prefab state; otherwise <see langword="false" />. </returns>
        private static bool TryResolvePreviewPrefabMirrorSourceObjectOverride (
            UnityEngine.Object sourceObject,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? previewObject)
        {
            previewObject = null;
            if (!TryGetPrefabAssetPath(sourceObject, out var prefabPath))
            {
                return false;
            }

            if (!executionContext.TryResolveTemporaryPrefabPreviewObject(prefabPath, sourceObject, out previewObject))
            {
                previewObject = null;
                return false;
            }

            if (previewObject == null)
            {
                previewObject = null;
                return false;
            }

            return true;
        }

        /// <summary> Tries to resolve the prefab asset path that owns one prefab asset object or prefab-stage object. </summary>
        /// <param name="unityObject"> The prefab-related Unity object. </param>
        /// <param name="prefabPath"> The normalized prefab asset path when found. </param>
        /// <returns> <see langword="true" /> when the object can be associated with an existing prefab asset path; otherwise <see langword="false" />. </returns>
        private static bool TryGetPrefabAssetPath (
            UnityEngine.Object unityObject,
            out string prefabPath)
        {
            prefabPath = string.Empty;
            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (!PrefabAssetPath.TryParse(assetPath, out var typedPrefabPath)
                || !PrefabOperationUtilities.TryEnsurePrefabAssetExists(typedPrefabPath, out _))
            {
                return false;
            }

            prefabPath = typedPrefabPath.Value;
            return true;
        }

        private static bool TryResolveAssetObjectFromGuid (
            Guid assetGuid,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid.ToString("N"));
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                errorMessage = $"Asset GUID could not be resolved: {assetGuid}.";
                return false;
            }

            return TryResolveAssetObjectFromPath(
                assetPath,
                executionContext: null,
                allowTemporaryState: false,
                out unityObject,
                out errorMessage);
        }

        private static bool TryResolveAssetObjectFromPath (
            string assetPath,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            var normalizedAssetPath = PathStringNormalizer.ToSlashSeparated(assetPath);
            if (allowTemporaryState
                && executionContext != null
                && executionContext.TryGetPlannedAssetState(normalizedAssetPath, out var plannedAssetState)
                && plannedAssetState.UnityObject != null)
            {
                unityObject = plannedAssetState.UnityObject;
                errorMessage = string.Empty;
                return true;
            }

            unityObject = AssetDatabase.LoadMainAssetAtPath(normalizedAssetPath);
            if (unityObject == null)
            {
                errorMessage = $"Asset path could not be resolved to a main asset: {normalizedAssetPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveTargetObjectOrComponent (
            GameObject gameObject,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType,
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
                    componentType.Value,
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
        /// <param name="stableObjectId"> The normalized resolved reference when successful. </param>
        /// <param name="errorMessage"> The validation error message when the object has no stable editor identity. </param>
        /// <returns> <see langword="true" /> when a stable resolved reference can be created; otherwise <see langword="false" />. </returns>
        private static bool TryCreateGlobalObjectId (
            UnityEngine.Object unityObject,
            out UnityGlobalObjectId? stableObjectId,
            out string errorMessage)
        {
            if (!UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(unityObject, out stableObjectId))
            {
                errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryCreateGlobalObjectIdFromPreviewSceneObject (
            GameObject gameObject,
            OperationExecutionContext executionContext,
            out UnityGlobalObjectId? stableObjectId)
        {
            stableObjectId = null;
            if (!executionContext.TryResolveTemporaryScenePath(gameObject.scene, out var scenePath))
            {
                return false;
            }

            return TryCreateGlobalObjectIdFromTemporarySceneTarget(scenePath, gameObject, executionContext, out stableObjectId);
        }

        private static bool TryCreateGlobalObjectIdFromPreviewPrefabObject (
            GameObject gameObject,
            OperationExecutionContext executionContext,
            out UnityGlobalObjectId? stableObjectId)
        {
            stableObjectId = null;
            if (!executionContext.TryResolveTemporaryPrefabPath(gameObject, out var prefabPath))
            {
                return false;
            }

            return TryCreateGlobalObjectIdFromTemporaryPrefabTarget(prefabPath, gameObject, executionContext, out stableObjectId);
        }

        private static bool TryCreateGlobalObjectIdFromTemporarySceneTarget (
            string scenePath,
            UnityEngine.Object unityObject,
            OperationExecutionContext executionContext,
            out UnityGlobalObjectId? stableObjectId)
        {
            stableObjectId = null;
            if (executionContext.TryResolveTemporarySceneGlobalObjectId(scenePath, unityObject, out var globalObjectId))
            {
                stableObjectId = globalObjectId;
                return true;
            }

            if (TryCreateGlobalObjectId(unityObject, out stableObjectId, out _))
            {
                return true;
            }

            return executionContext.TryResolveTemporarySceneSourceObject(scenePath, unityObject, out var mirroredSourceObject)
                && mirroredSourceObject != null
                && TryCreateGlobalObjectId(mirroredSourceObject, out stableObjectId, out _);
        }

        private static bool TryCreateGlobalObjectIdFromTemporaryPrefabTarget (
            string prefabPath,
            UnityEngine.Object unityObject,
            OperationExecutionContext executionContext,
            out UnityGlobalObjectId? stableObjectId)
        {
            stableObjectId = null;
            if (executionContext.TryResolveTemporaryPrefabGlobalObjectId(prefabPath, unityObject, out var globalObjectId))
            {
                stableObjectId = globalObjectId;
                return true;
            }

            if (TryCreateGlobalObjectId(unityObject, out stableObjectId, out _))
            {
                return true;
            }

            return executionContext.TryResolveTemporaryPrefabSourceObject(prefabPath, unityObject, out var mirroredSourceObject)
                && mirroredSourceObject != null
                && TryCreateGlobalObjectIdFromPrefabMirrorSource(prefabPath, mirroredSourceObject, out stableObjectId, out _);
        }

        /// <summary> Creates one stable resolved reference from one prefab mirror object or its persisted prefab correspondence. </summary>
        /// <param name="prefabPath"> The prefab asset path used to search persisted prefab correspondence. </param>
        /// <param name="unityObject"> The prefab mirror object, opened-stage object, or persisted prefab object. </param>
        /// <param name="stableObjectId"> The stable resolved reference when the object or one of its persisted correspondences exposes a GlobalObjectId. </param>
        /// <param name="errorMessage"> The validation error message when no stable prefab correspondence can be normalized. </param>
        /// <returns> <see langword="true" /> when a stable resolved reference can be created; otherwise <see langword="false" />. </returns>
        private static bool TryCreateGlobalObjectIdFromPrefabMirrorSource (
            string prefabPath,
            UnityEngine.Object unityObject,
            out UnityGlobalObjectId? stableObjectId,
            out string errorMessage)
        {
            if (TryCreateGlobalObjectId(unityObject, out stableObjectId, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            var prefabSourceAtPath = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(unityObject, prefabPath);
            if (prefabSourceAtPath != null
                && TryCreateGlobalObjectId(prefabSourceAtPath, out stableObjectId, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            var prefabSourceObject = PrefabUtility.GetCorrespondingObjectFromSource(unityObject);
            if (prefabSourceObject != null
                && TryCreateGlobalObjectId(prefabSourceObject, out stableObjectId, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            var originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(unityObject);
            if (originalSource != null
                && TryCreateGlobalObjectId(originalSource, out stableObjectId, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = "Resolved target does not expose a stable GlobalObjectId in the current editor state.";
            return false;
        }
    }
}
