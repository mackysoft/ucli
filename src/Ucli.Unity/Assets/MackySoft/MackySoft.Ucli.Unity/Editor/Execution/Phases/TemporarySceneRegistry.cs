using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks request-local preview scenes used as persisted scene sandboxes during plan execution. </summary>
    internal sealed class TemporarySceneRegistry
    {
        private readonly Dictionary<string, Scene> previewScenesByPath =
            new Dictionary<string, Scene>(StringComparer.Ordinal);

        /// <summary> Tries to get one tracked preview scene by asset path. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        /// <param name="scene"> The tracked preview scene when found. </param>
        /// <returns> <see langword="true" /> when a live preview scene is already tracked for <paramref name="scenePath" />; otherwise <see langword="false" />. </returns>
        public bool TryGetPreviewScene (
            string scenePath,
            out Scene scene)
        {
            scene = default;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }

            if (!previewScenesByPath.TryGetValue(scenePath, out var value))
            {
                return false;
            }

            if (!value.IsValid() || !value.isLoaded || !EditorSceneManager.IsPreviewScene(value))
            {
                previewScenesByPath.Remove(scenePath);
                return false;
            }

            scene = value;
            return true;
        }

        /// <summary> Tries to resolve one tracked preview scene back to its logical scene asset path. </summary>
        /// <param name="scene"> The preview scene instance. </param>
        /// <param name="scenePath"> The logical scene asset path when the preview scene is tracked. </param>
        /// <returns> <see langword="true" /> when <paramref name="scene" /> is a tracked preview scene; otherwise <see langword="false" />. </returns>
        public bool TryResolvePreviewScenePath (
            Scene scene,
            out string scenePath)
        {
            scenePath = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded || !EditorSceneManager.IsPreviewScene(scene))
            {
                return false;
            }

            foreach (var pair in previewScenesByPath)
            {
                var trackedScene = pair.Value;
                if (!trackedScene.IsValid() || !trackedScene.isLoaded || !EditorSceneManager.IsPreviewScene(trackedScene))
                {
                    continue;
                }

                if (trackedScene.handle != scene.handle)
                {
                    continue;
                }

                scenePath = pair.Key;
                return true;
            }

            return false;
        }

        /// <summary> Gets one tracked preview scene or opens it from persisted asset contents when needed. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        /// <param name="scene"> The tracked or newly opened preview scene when successful. </param>
        /// <param name="errorMessage"> The error message when preview scene creation fails. </param>
        /// <returns> <see langword="true" /> when the preview scene is available; otherwise <see langword="false" />. </returns>
        public bool TryGetOrOpenPreviewScene (
            string scenePath,
            out Scene scene,
            out string errorMessage)
        {
            if (TryGetPreviewScene(scenePath, out scene))
            {
                errorMessage = string.Empty;
                return true;
            }

            try
            {
                scene = EditorSceneManager.OpenPreviewScene(scenePath);
            }
            catch (Exception exception)
            {
                scene = default;
                errorMessage = $"Scene preview could not be opened: {scenePath}. {exception.Message}";
                return false;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                errorMessage = $"Scene preview could not be opened: {scenePath}.";
                return false;
            }

            previewScenesByPath[scenePath] = scene;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Closes all tracked preview scenes and clears request-local state. </summary>
        public void Clear ()
        {
            foreach (var pair in previewScenesByPath)
            {
                var previewScene = pair.Value;
                if (!previewScene.IsValid() || !previewScene.isLoaded || !EditorSceneManager.IsPreviewScene(previewScene))
                {
                    continue;
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            previewScenesByPath.Clear();
        }
    }
}
