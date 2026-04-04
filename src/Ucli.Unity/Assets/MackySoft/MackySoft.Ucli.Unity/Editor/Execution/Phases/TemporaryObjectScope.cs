using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks temporary Unity objects and prefab contents roots that must be cleaned at request end. </summary>
    internal sealed class TemporaryObjectScope
    {
        private readonly Dictionary<string, TemporaryPrefabContentsRoot> temporaryPrefabContentsRootsByPath =
            new Dictionary<string, TemporaryPrefabContentsRoot>(StringComparer.Ordinal);

        private readonly List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();

        public void TrackTemporaryPrefabContentsRoot (
            string prefabPath,
            GameObject prefabContentsRoot)
        {
            TrackTemporaryPrefabContentsRoot(
                prefabPath,
                prefabContentsRoot,
                TemporaryPrefabCleanupKind.UnloadPrefabContents);
        }

        public bool TryCloneTemporaryPrefabContentsRootFromOpenedStage (
            string prefabPath,
            GameObject openedPrefabContentsRoot,
            out GameObject? prefabContentsRoot,
            out string errorMessage)
        {
            prefabContentsRoot = null;
            if (TryGetTemporaryPrefabContentsRoot(prefabPath, out prefabContentsRoot)
                && prefabContentsRoot != null)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path must not be null, empty, or whitespace.", nameof(prefabPath));
            }

            if (openedPrefabContentsRoot == null)
            {
                throw new ArgumentNullException(nameof(openedPrefabContentsRoot));
            }

            Scene previewScene;
            try
            {
                previewScene = EditorSceneManager.NewPreviewScene();
            }
            catch (Exception exception)
            {
                errorMessage = $"Prefab preview could not be created: {prefabPath}. {exception.Message}";
                return false;
            }

            if (!previewScene.IsValid() || !previewScene.isLoaded)
            {
                errorMessage = $"Prefab preview could not be created: {prefabPath}.";
                return false;
            }

            try
            {
                prefabContentsRoot = UnityEngine.Object.Instantiate(openedPrefabContentsRoot);
                prefabContentsRoot.name = openedPrefabContentsRoot.name;
                SceneManager.MoveGameObjectToScene(prefabContentsRoot, previewScene);
            }
            catch (Exception exception)
            {
                if (prefabContentsRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefabContentsRoot);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
                prefabContentsRoot = null;
                errorMessage = $"Prefab preview could not mirror opened prefab state: {prefabPath}. {exception.Message}";
                return false;
            }

            TrackTemporaryPrefabContentsRoot(
                prefabPath,
                prefabContentsRoot,
                TemporaryPrefabCleanupKind.ClosePreviewScene);
            errorMessage = string.Empty;
            return true;
        }

        public bool TryGetTemporaryPrefabContentsRoot (
            string prefabPath,
            out GameObject? prefabContentsRoot)
        {
            prefabContentsRoot = null;
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                return false;
            }

            if (!temporaryPrefabContentsRootsByPath.TryGetValue(prefabPath, out var value))
            {
                return false;
            }

            if (value.PrefabContentsRoot == null)
            {
                temporaryPrefabContentsRootsByPath.Remove(prefabPath);
                return false;
            }

            prefabContentsRoot = value.PrefabContentsRoot;
            return true;
        }

        public bool TryResolveTemporaryPrefabPath (
            GameObject gameObject,
            out string prefabPath)
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            foreach (var pair in temporaryPrefabContentsRootsByPath)
            {
                var prefabContentsRoot = pair.Value.PrefabContentsRoot;
                if (prefabContentsRoot == null)
                {
                    continue;
                }

                if (gameObject == prefabContentsRoot
                    || gameObject.transform.IsChildOf(prefabContentsRoot.transform))
                {
                    prefabPath = pair.Key;
                    return true;
                }
            }

            prefabPath = string.Empty;
            return false;
        }

        public void TrackTemporaryObject (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            temporaryObjects.Add(unityObject);
        }

        public bool ContainsTemporaryObject (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            for (var i = 0; i < temporaryObjects.Count; i++)
            {
                if (ReferenceEquals(temporaryObjects[i], unityObject))
                {
                    return true;
                }
            }

            return false;
        }

        public void Cleanup ()
        {
            foreach (var pair in temporaryPrefabContentsRootsByPath)
            {
                var prefabContentsRoot = pair.Value.PrefabContentsRoot;
                if (prefabContentsRoot != null)
                {
                    switch (pair.Value.CleanupKind)
                    {
                        case TemporaryPrefabCleanupKind.UnloadPrefabContents:
                            UnityEditor.PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
                            break;

                        case TemporaryPrefabCleanupKind.ClosePreviewScene:
                            var previewScene = prefabContentsRoot.scene;
                            if (previewScene.IsValid() && previewScene.isLoaded && EditorSceneManager.IsPreviewScene(previewScene))
                            {
                                EditorSceneManager.ClosePreviewScene(previewScene);
                            }

                            break;
                    }
                }
            }

            temporaryPrefabContentsRootsByPath.Clear();

            for (var i = temporaryObjects.Count - 1; i >= 0; i--)
            {
                var temporaryObject = temporaryObjects[i];
                if (temporaryObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporaryObject);
                }
            }

            temporaryObjects.Clear();
        }

        private void TrackTemporaryPrefabContentsRoot (
            string prefabPath,
            GameObject prefabContentsRoot,
            TemporaryPrefabCleanupKind cleanupKind)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path must not be null, empty, or whitespace.", nameof(prefabPath));
            }

            if (prefabContentsRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabContentsRoot));
            }

            temporaryPrefabContentsRootsByPath[prefabPath] =
                new TemporaryPrefabContentsRoot(prefabContentsRoot, cleanupKind);
        }

        private readonly struct TemporaryPrefabContentsRoot
        {
            public TemporaryPrefabContentsRoot (
                GameObject prefabContentsRoot,
                TemporaryPrefabCleanupKind cleanupKind)
            {
                PrefabContentsRoot = prefabContentsRoot;
                CleanupKind = cleanupKind;
            }

            public GameObject PrefabContentsRoot { get; }

            public TemporaryPrefabCleanupKind CleanupKind { get; }
        }

        private enum TemporaryPrefabCleanupKind
        {
            UnloadPrefabContents = 0,
            ClosePreviewScene = 1,
        }
    }
}
