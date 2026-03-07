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
        /// <param name="gameObject"> The resolved GameObject when successful. </param>
        /// <param name="scene"> The owning loaded scene when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one loaded-scene GameObject; otherwise <see langword="false" />. </returns>
        public static bool TryResolveLoadedSceneGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            out GameObject? gameObject,
            out Scene scene,
            out string errorMessage)
        {
            gameObject = null;
            scene = default;
            if (!UnityObjectReferenceResolver.TryResolveGameObject(reference, executionContext, out gameObject, out errorMessage))
            {
                return false;
            }

            return TryGetLoadedSceneFromGameObject(gameObject!, out scene, out errorMessage);
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
    }
}