using System;
using UnityEditor;
using UnityEditor.SceneManagement;
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
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
            {
                errorMessage = $"Scene is not loaded: {scenePath}. Use 'ucli.scene.open' first.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Validates that opening one live scene will not be blocked by dirty live editor state. </summary>
        /// <param name="scenePath"> The target scene path. </param>
        /// <param name="errorMessage"> The validation error message when blocked. </param>
        /// <returns> <see langword="true" /> when the scene can be opened without dirty-state blockers; otherwise <see langword="false" />. </returns>
        public static bool TryEnsureCanOpenSceneLive (
            string scenePath,
            out string errorMessage)
        {
            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentPrefabStage != null
                && currentPrefabStage.scene.isDirty)
            {
                errorMessage = $"Dirty prefab stage blocks opening scene '{scenePath}': {currentPrefabStage.assetPath}.";
                return false;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var loadedScene = SceneManager.GetSceneAt(i);
                if (!loadedScene.IsValid()
                    || !loadedScene.isLoaded
                    || EditorSceneManager.IsPreviewScene(loadedScene)
                    || !loadedScene.isDirty
                    || string.Equals(loadedScene.path, scenePath, StringComparison.Ordinal))
                {
                    continue;
                }

                errorMessage = $"Dirty loaded scene blocks opening scene '{scenePath}': {CreateSceneDisplayName(loadedScene)}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static string CreateSceneDisplayName (Scene scene)
        {
            return string.IsNullOrWhiteSpace(scene.path)
                ? scene.name
                : scene.path;
        }
    }
}
