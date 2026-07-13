using MackySoft.Ucli.Unity.SceneInspection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers shared by GameObject-domain operations. </summary>
    internal static class GoOperationUtilities
    {
        /// <summary> Resolves one scene path to a loaded scene. </summary>
        /// <param name="scenePath"> The project-relative scene path. </param>
        /// <param name="scene"> The resolved loaded scene when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the scene exists and is loaded; otherwise <see langword="false" />. </returns>
        public static bool TryResolveLoadedScene (
            string scenePath,
            out Scene scene,
            out string errorMessage)
        {
            scene = default;
            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
            }

            return SceneAssetSourceUtilities.TryGetLoadedScene(scenePath, out scene, out errorMessage);
        }

        /// <summary> Resolves one scene path to the appropriate runtime scene for the current phase. </summary>
        /// <param name="scenePath"> The project-relative scene path. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to use request-local preview scene state that was explicitly prepared earlier in the same request. </param>
        /// <param name="scene"> The resolved scene when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the scene can be resolved for the requested phase; otherwise <see langword="false" />. </returns>
        public static bool TryResolveScene (
            string scenePath,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out Scene scene,
            out string errorMessage)
        {
            scene = default;
            if (executionContext == null)
            {
                throw new System.ArgumentNullException(nameof(executionContext));
            }

            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
            }

            if (allowTemporaryState
                && executionContext.TryGetTemporaryScene(scenePath, out scene))
            {
                errorMessage = string.Empty;
                return true;
            }

            if (!SceneAssetSourceUtilities.TryGetLoadedScene(scenePath, out scene, out errorMessage))
            {
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Resolves one reference to a GameObject that belongs to an editable scene or opened prefab resource. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="resolutionPolicy"> The request-local state participation policy. </param>
        /// <param name="resolution"> The editable GameObject resolution when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one editable GameObject; otherwise <see langword="false" />. </returns>
        public static bool TryResolveEditableGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            OperationObjectReferenceUtilities.ReferenceResolutionPolicy resolutionPolicy,
            out EditableGameObjectResolutionState resolution,
            out string errorMessage)
        {
            resolution = default;
            if (!OperationObjectReferenceUtilities.TryResolveUnityObject(
                    reference,
                    executionContext,
                    resolutionPolicy,
                    out var objectResolution,
                    out errorMessage))
            {
                return false;
            }

            var gameObject = objectResolution.UnityObject as GameObject;
            if (gameObject == null)
            {
                errorMessage = "Reference did not resolve to a GameObject.";
                return false;
            }

            if (objectResolution.TemporaryAliasResource is OperationResource temporaryAliasResource)
            {
                resolution = new EditableGameObjectResolutionState(gameObject, temporaryAliasResource);
                errorMessage = string.Empty;
                return true;
            }

            if (!OperationResourceUtilities.TryResolveOwnerResource(gameObject, executionContext, out var resource, out errorMessage))
            {
                return false;
            }

            resolution = new EditableGameObjectResolutionState(gameObject, resource);
            return true;
        }

        /// <summary> Marks one request-local scene resource dirty after a successful plan-time hierarchy mutation. </summary>
        /// <param name="resource"> The mutated resource. </param>
        /// <param name="executionContext"> The request execution context. </param>
        public static void MarkPlanResourceDirty (
            OperationResource resource,
            OperationExecutionContext executionContext)
        {
            if (executionContext == null)
            {
                throw new System.ArgumentNullException(nameof(executionContext));
            }

            if (resource.Kind != OperationTouchKind.Scene)
            {
                return;
            }

            if (!executionContext.TryGetTemporaryScene(resource.Path, out var temporaryScene))
            {
                return;
            }

            if (!temporaryScene.IsValid() || !temporaryScene.isLoaded)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(temporaryScene);
        }

        /// <summary> Creates one temporary GameObject for plan-time aliasing. </summary>
        /// <param name="name"> The GameObject name. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <returns> The created temporary GameObject. </returns>
        public static GameObject CreateTemporaryGameObject (
            string name,
            OperationExecutionContext executionContext)
        {
            if (executionContext == null)
            {
                throw new System.ArgumentNullException(nameof(executionContext));
            }

            var temporaryGameObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            };
            executionContext.TrackTemporaryObject(temporaryGameObject);
            return temporaryGameObject;
        }

        /// <summary> Verifies that one GameObject belongs to request-local plan state that can be safely mutated. </summary>
        /// <param name="gameObject"> The candidate GameObject. </param>
        /// <param name="resource"> The owning resource recorded for the candidate. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="errorMessage"> The validation error message when the GameObject is not request-local. </param>
        /// <returns> <see langword="true" /> when <paramref name="gameObject" /> can be mutated during plan execution; otherwise <see langword="false" />. </returns>
        public static bool TryEnsureRequestLocalPlanGameObject (
            GameObject gameObject,
            OperationResource resource,
            OperationExecutionContext executionContext,
            out string errorMessage)
        {
            if (gameObject == null)
            {
                throw new System.ArgumentNullException(nameof(gameObject));
            }

            if (executionContext == null)
            {
                throw new System.ArgumentNullException(nameof(executionContext));
            }

            if (executionContext.IsTrackedTemporaryObject(gameObject))
            {
                errorMessage = string.Empty;
                return true;
            }

            switch (resource.Kind)
            {
                case OperationTouchKind.Scene:
                    if (executionContext.TryGetTemporaryScene(resource.Path, out var temporaryScene)
                        && gameObject.scene == temporaryScene)
                    {
                        errorMessage = string.Empty;
                        return true;
                    }

                    break;

                case OperationTouchKind.Prefab:
                    if (executionContext.TryGetTemporaryPrefabContentsRoot(resource.Path, out var prefabContentsRoot)
                        && prefabContentsRoot != null
                        && gameObject.scene == prefabContentsRoot.scene)
                    {
                        errorMessage = string.Empty;
                        return true;
                    }

                    break;
            }

            errorMessage = $"GameObject could not be projected into request-local plan state: {resource.Path}.";
            return false;
        }

        /// <summary> Ensures that one resource has request-local plan state available without widening the raw primitive contract beyond live editor state. </summary>
        /// <param name="resource"> The resource that one plan-time mutation will touch. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="errorMessage"> The validation error message when request-local plan state cannot be prepared. </param>
        /// <returns> <see langword="true" /> when request-local plan state is available for the resource; otherwise <see langword="false" />. </returns>
        public static bool TryEnsurePlanResourceState (
            OperationResource resource,
            OperationExecutionContext executionContext,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new System.ArgumentNullException(nameof(executionContext));
            }

            switch (resource.Kind)
            {
                case OperationTouchKind.Scene:
                    if (executionContext.TryGetTemporaryScene(resource.Path, out _))
                    {
                        errorMessage = string.Empty;
                        return true;
                    }

                    if (!SceneAssetSourceUtilities.TryGetLoadedScene(resource.Path, out _, out errorMessage))
                    {
                        return false;
                    }

                    return executionContext.TryEnsureSceneExecutionSession(resource.Path, out errorMessage);

                case OperationTouchKind.Prefab:
                    if (executionContext.TryGetTemporaryPrefabContentsRoot(resource.Path, out var prefabContentsRoot)
                        && prefabContentsRoot != null)
                    {
                        errorMessage = string.Empty;
                        return true;
                    }

                    if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(resource.Path, out _, out _))
                    {
                        errorMessage = $"Prefab is not opened: {resource.Path}. Use 'ucli.prefab.open' first.";
                        return false;
                    }

                    return executionContext.TryEnsurePrefabExecutionSession(resource.Path, out errorMessage);

                default:
                    errorMessage = $"Operation does not support plan-time GameObject state for resource kind '{resource.Kind}'.";
                    return false;
            }
        }

        internal readonly struct EditableGameObjectResolutionState
        {
            public EditableGameObjectResolutionState (
                GameObject gameObject,
                OperationResource resource)
            {
                GameObject = gameObject;
                Resource = resource;
            }

            public GameObject GameObject { get; }

            public OperationResource Resource { get; }
        }

    }
}
