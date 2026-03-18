using UnityEditor;
using UnityEngine.SceneManagement;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable scene-operation helpers shared by scene-domain phase operations. </summary>
    internal static class SceneOperationUtilities
    {
        /// <summary> Validates that the specified path resolves to a scene asset. </summary>
        /// <param name="scenePath"> The scene path. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when scene asset exists; otherwise <see langword="false" />. </returns>
        public static bool TryEnsureSceneAssetExists (
            string scenePath,
            out string errorMessage)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
            {
                errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to resolve one loaded scene by project-relative path. </summary>
        /// <param name="scenePath"> The scene path. </param>
        /// <param name="scene"> The resolved loaded scene when successful. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when loaded scene is found; otherwise <see langword="false" />. </returns>
        public static bool TryGetLoadedScene (
            string scenePath,
            out Scene scene,
            out string errorMessage)
        {
            scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                errorMessage = $"Scene is not loaded: {scenePath}. Use 'ucli.scene.open' first.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Creates one touched entry for the specified scene path. </summary>
        /// <param name="scenePath"> The scene path. </param>
        /// <returns> The touched scene entry. </returns>
        public static OperationTouch CreateSceneTouch (string scenePath)
        {
            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            return new OperationTouch(
                Kind: OperationTouchKind.Scene,
                Path: scenePath,
                Guid: string.IsNullOrWhiteSpace(sceneGuid) ? null : sceneGuid);
        }
    }
}