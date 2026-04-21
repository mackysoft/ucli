using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Project
{
    /// <summary> Opens one persisted scene asset as a transient preview scene and closes it on disposal. </summary>
    internal readonly struct PersistedPreviewSceneLease : IDisposable
    {
        /// <summary> Initializes a new instance of the <see cref="PersistedPreviewSceneLease" /> struct. </summary>
        /// <param name="scenePath"> The logical scene asset path. </param>
        /// <param name="scene"> The opened preview scene. </param>
        public PersistedPreviewSceneLease (
            string scenePath,
            Scene scene)
        {
            ScenePath = scenePath;
            Scene = scene;
        }

        /// <summary> Gets the logical scene asset path associated with the opened preview scene. </summary>
        public string ScenePath { get; }

        /// <summary> Gets the opened preview scene. </summary>
        public Scene Scene { get; }

        /// <summary> Tries to open one persisted scene asset as a preview scene. </summary>
        /// <param name="scenePath"> The project-relative scene asset path. </param>
        /// <param name="lease"> The opened preview-scene lease when successful. </param>
        /// <param name="errorMessage"> The validation or open failure message when unsuccessful. </param>
        /// <returns> <see langword="true" /> when the preview scene is opened; otherwise <see langword="false" />. </returns>
        public static bool TryOpen (
            string scenePath,
            out PersistedPreviewSceneLease lease,
            out string errorMessage)
        {
            lease = default;

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
            {
                errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
                return false;
            }

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(scenePath);
            }
            catch (Exception exception)
            {
                errorMessage = $"Scene could not be opened for query: {scenePath}. {exception.Message}";
                return false;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                errorMessage = $"Scene could not be opened for query: {scenePath}.";
                return false;
            }

            lease = new PersistedPreviewSceneLease(scenePath, scene);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Closes one preview scene when it is still open. </summary>
        /// <param name="scene"> The candidate preview scene. </param>
        public static void CloseIfNeeded (Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !EditorSceneManager.IsPreviewScene(scene))
            {
                return;
            }

            EditorSceneManager.ClosePreviewScene(scene);
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            CloseIfNeeded(Scene);
        }
    }
}
