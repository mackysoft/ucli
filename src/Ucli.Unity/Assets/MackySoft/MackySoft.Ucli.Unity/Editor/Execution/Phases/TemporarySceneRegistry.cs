using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks request-local preview scenes used as persisted scene sandboxes during plan execution. </summary>
    internal sealed class TemporarySceneRegistry
    {
        private readonly Dictionary<string, TemporaryPreviewScene> previewScenesByPath =
            new Dictionary<string, TemporaryPreviewScene>(StringComparer.Ordinal);

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

            if (!value.PreviewScene.IsValid() || !value.PreviewScene.isLoaded || !EditorSceneManager.IsPreviewScene(value.PreviewScene))
            {
                previewScenesByPath.Remove(scenePath);
                return false;
            }

            scene = value.PreviewScene;
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
                var trackedScene = pair.Value.PreviewScene;
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

        /// <summary> Tries to resolve one mirrored preview object back to its live scene source object. </summary>
        /// <param name="scenePath"> The tracked logical scene path. </param>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="sourceObject"> The mirrored live source object when found. </param>
        /// <returns> <see langword="true" /> when the preview object originated from one dirty loaded-scene mirror; otherwise <see langword="false" />. </returns>
        public bool TryResolveMirroredSourceObject (
            string scenePath,
            UnityEngine.Object previewObject,
            out UnityEngine.Object? sourceObject)
        {
            sourceObject = null;
            if (previewObject == null)
            {
                throw new ArgumentNullException(nameof(previewObject));
            }

            if (!previewScenesByPath.TryGetValue(scenePath, out var previewSceneState))
            {
                return false;
            }

            if (previewSceneState.MirrorMapping == null)
            {
                return false;
            }

            return previewSceneState.MirrorMapping.TryGetSourceObject(previewObject, out sourceObject);
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

            previewScenesByPath[scenePath] = new TemporaryPreviewScene(scene, mirrorMapping: null);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Gets one tracked preview scene or clones one currently loaded scene snapshot into a new preview scene when needed. </summary>
        /// <param name="scenePath"> The logical scene asset path. </param>
        /// <param name="sourceScene"> The loaded live scene whose current snapshot should be mirrored. </param>
        /// <param name="scene"> The tracked or newly cloned preview scene when successful. </param>
        /// <param name="errorMessage"> The error message when preview scene creation fails. </param>
        /// <returns> <see langword="true" /> when the preview scene is available; otherwise <see langword="false" />. </returns>
        public bool TryGetOrCreatePreviewSceneFromLoadedScene (
            string scenePath,
            Scene sourceScene,
            out Scene scene,
            out string errorMessage)
        {
            if (TryGetPreviewScene(scenePath, out scene))
            {
                errorMessage = string.Empty;
                return true;
            }

            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                scene = default;
                errorMessage = $"Loaded scene could not be mirrored into request-local preview state: {scenePath}.";
                return false;
            }

            if (!TryCreateEmptyPreviewScene(out scene, out errorMessage))
            {
                return false;
            }

            try
            {
                // NOTE:
                // When the editor scene is dirty, plan execution must observe the same hierarchy snapshot
                // that selection already resolved against. Mirror the current live hierarchy into one
                // request-local preview scene instead of reopening persisted asset contents.
                var mirrorMapping = new TemporaryMirrorMapping();
                var roots = sourceScene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    var clonedRoot = UnityEngine.Object.Instantiate(roots[i]);
                    clonedRoot.name = roots[i].name;
                    SceneManager.MoveGameObjectToScene(clonedRoot, scene);
                    if (!TemporaryMirrorUtilities.TryRegisterHierarchyMirror(roots[i], clonedRoot, mirrorMapping, out errorMessage))
                    {
                        TryClosePreviewScene(scene);
                        scene = default;
                        return false;
                    }
                }

                if (!TemporaryMirrorUtilities.TryRebindMirroredLocalReferences(mirrorMapping, out errorMessage))
                {
                    TryClosePreviewScene(scene);
                    scene = default;
                    return false;
                }

                if (sourceScene.isDirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                previewScenesByPath[scenePath] = new TemporaryPreviewScene(scene, mirrorMapping);
            }
            catch (Exception exception)
            {
                TryClosePreviewScene(scene);
                scene = default;
                errorMessage = $"Scene preview could not mirror the loaded scene state: {scenePath}. {exception.Message}";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Closes all tracked preview scenes and clears request-local state. </summary>
        public void Clear ()
        {
            foreach (var pair in previewScenesByPath)
            {
                var previewScene = pair.Value.PreviewScene;
                TryClosePreviewScene(previewScene);
            }

            previewScenesByPath.Clear();
        }

        private static bool TryCreateEmptyPreviewScene (
            out Scene scene,
            out string errorMessage)
        {
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
            }
            catch (Exception exception)
            {
                scene = default;
                errorMessage = $"Scene preview could not be created. {exception.Message}";
                return false;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                errorMessage = "Scene preview could not be created.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static void TryClosePreviewScene (Scene previewScene)
        {
            if (!previewScene.IsValid() || !previewScene.isLoaded || !EditorSceneManager.IsPreviewScene(previewScene))
            {
                return;
            }

            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        private readonly struct TemporaryPreviewScene
        {
            public TemporaryPreviewScene (
                Scene previewScene,
                TemporaryMirrorMapping? mirrorMapping)
            {
                PreviewScene = previewScene;
                MirrorMapping = mirrorMapping;
            }

            public Scene PreviewScene { get; }

            public TemporaryMirrorMapping? MirrorMapping { get; }
        }
    }
}
