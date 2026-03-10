using UnityEngine;
using UnityEngine.SceneManagement;

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
            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
            }

            return SceneOperationUtilities.TryGetLoadedScene(scenePath, out scene, out errorMessage);
        }

        /// <summary> Resolves one reference to a GameObject that belongs to a loaded scene. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="resolution"> The loaded-scene GameObject resolution when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one loaded-scene GameObject; otherwise <see langword="false" />. </returns>
        public static bool TryResolveLoadedSceneGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            out LoadedSceneGameObjectResolutionState resolution,
            out string errorMessage)
        {
            return TryResolveLoadedSceneGameObject(
                reference,
                executionContext,
                allowTemporaryState: true,
                out resolution,
                out errorMessage);
        }

        /// <summary> Resolves one reference to a GameObject that belongs to a loaded scene. Temporary aliases can be enabled when required. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan aliases may satisfy the reference. </param>
        /// <param name="resolution"> The loaded-scene GameObject resolution when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one loaded-scene GameObject; otherwise <see langword="false" />. </returns>
        public static bool TryResolveLoadedSceneGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out LoadedSceneGameObjectResolutionState resolution,
            out string errorMessage)
        {
            resolution = default;
            if (!TryResolveEditableGameObject(
                reference,
                executionContext,
                allowTemporaryState,
                out var editableResolution,
                out errorMessage))
            {
                return false;
            }

            if (editableResolution.Resource.Kind != OperationTouchKind.Scene)
            {
                errorMessage = "GameObject is not part of a loaded scene.";
                return false;
            }

            if (!TryGetLoadedSceneFromGameObject(editableResolution.GameObject!, out var scene, out errorMessage))
            {
                return false;
            }

            resolution = new LoadedSceneGameObjectResolutionState(editableResolution.GameObject!, scene);
            return true;
        }

        /// <summary> Resolves one reference to a GameObject that belongs to an editable scene or opened prefab resource. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan aliases may satisfy the reference. </param>
        /// <param name="resolution"> The editable GameObject resolution when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one editable GameObject; otherwise <see langword="false" />. </returns>
        public static bool TryResolveEditableGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out EditableGameObjectResolutionState resolution,
            out string errorMessage)
        {
            resolution = default;
            if (reference.Kind == UnityObjectReferenceKind.Alias
                && executionContext.TryGetTemporaryAliasState(reference.Alias!, out var temporaryAliasState))
            {
                var temporaryGameObject = temporaryAliasState.UnityObject as GameObject;
                if (temporaryGameObject == null)
                {
                    errorMessage = "Reference did not resolve to a GameObject.";
                    return false;
                }

                resolution = new EditableGameObjectResolutionState(temporaryGameObject, temporaryAliasState.Resource);
                errorMessage = string.Empty;
                return true;
            }

            if (!UnityObjectReferenceResolver.TryResolveGameObject(reference, executionContext, out var gameObject, out errorMessage))
            {
                return false;
            }

            if (!OperationResourceUtilities.TryResolveOwnerResource(gameObject!, out var resource, out errorMessage))
            {
                return false;
            }

            resolution = new EditableGameObjectResolutionState(gameObject!, resource);
            return true;
        }

        /// <summary> Resolves the owning scene for one GameObject and ensures the scene is loaded. </summary>
        /// <param name="gameObject"> The GameObject whose owning scene is required. </param>
        /// <param name="scene"> The owning loaded scene when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the GameObject belongs to a loaded scene; otherwise <see langword="false" />. </returns>
        public static bool TryGetLoadedSceneFromGameObject (
            GameObject gameObject,
            out Scene scene,
            out string errorMessage)
        {
            scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                errorMessage = "GameObject is not part of a loaded scene.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
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

        internal readonly struct EditableGameObjectResolutionState
        {
            public EditableGameObjectResolutionState (
                GameObject gameObject,
                OperationResource resource)
            {
                GameObject = gameObject;
                Resource = resource;
            }

            public GameObject? GameObject { get; }

            public OperationResource Resource { get; }
        }

        internal readonly struct LoadedSceneGameObjectResolutionState
        {
            public LoadedSceneGameObjectResolutionState (
                GameObject gameObject,
                Scene scene)
            {
                GameObject = gameObject;
                Scene = scene;
            }

            public GameObject? GameObject { get; }

            public Scene Scene { get; }
        }
    }
}