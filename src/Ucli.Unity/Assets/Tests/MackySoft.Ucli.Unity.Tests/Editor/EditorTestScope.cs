using System;
using System.Collections.Generic;
using System.IO;
using MackySoft.Ucli.Unity.Execution.Phases;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Owns temporary editor-side test resources and releases them in one place. </summary>
    internal sealed class EditorTestScope : IDisposable
    {
        private readonly HashSet<string> assetPaths = new HashSet<string>(StringComparer.Ordinal);

        private readonly List<IDisposable> trackedDisposables = new List<IDisposable>();

        private readonly List<GameObject> trackedPrefabContentsRoots = new List<GameObject>();

        private readonly List<UnityEngine.Object> trackedUnityObjects = new List<UnityEngine.Object>();

        private bool closePrefabStage;

        private bool resetEditorSceneState;

        private bool disposed;

        /// <summary> Creates one temporary scene asset path that this scope will delete during cleanup. </summary>
        /// <param name="prefix"> The file-name prefix used to identify the test owner. </param>
        /// <returns> The project-relative scene asset path. </returns>
        public string CreateScenePath (string prefix)
        {
            resetEditorSceneState = true;
            return CreateAssetPath(prefix, ".unity");
        }

        /// <summary> Creates one temporary prefab asset path that this scope will delete during cleanup. </summary>
        /// <param name="prefix"> The file-name prefix used to identify the test owner. </param>
        /// <returns> The project-relative prefab asset path. </returns>
        public string CreatePrefabPath (string prefix)
        {
            return CreateAssetPath(prefix, ".prefab");
        }

        /// <summary> Creates one temporary asset path that this scope will delete during cleanup. </summary>
        /// <param name="prefix"> The file-name prefix used to identify the test owner. </param>
        /// <param name="extension"> The file extension including the leading dot. </param>
        /// <returns> The project-relative asset path. </returns>
        public string CreateAssetPath (
            string prefix,
            string extension = ".asset")
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("Asset path prefix must not be empty.", nameof(prefix));
            }

            if (string.IsNullOrWhiteSpace(extension) || extension[0] != '.')
            {
                throw new ArgumentException("Asset path extension must start with '.'.", nameof(extension));
            }

            var assetPath = $"Assets/{prefix}_{Guid.NewGuid():N}{extension}";
            assetPaths.Add(assetPath);
            return assetPath;
        }

        /// <summary> Creates one request execution context owned by this scope. </summary>
        /// <returns> The tracked execution context. </returns>
        public OperationExecutionContext CreateExecutionContext ()
        {
            var context = new OperationExecutionContext();
            trackedDisposables.Add(context);
            return context;
        }

        /// <summary> Creates one persisted ScriptableObject asset owned by this scope. </summary>
        /// <typeparam name="TAsset"> The ScriptableObject asset type. </typeparam>
        /// <param name="prefix"> The file-name prefix used to identify the test owner. </param>
        /// <param name="assetPath"> The created project-relative asset path. </param>
        /// <returns> The created ScriptableObject asset instance. </returns>
        public TAsset CreateScriptableAsset<TAsset> (
            string prefix,
            out string assetPath)
            where TAsset : ScriptableObject
        {
            assetPath = CreateAssetPath(prefix);
            var asset = ScriptableObject.CreateInstance<TAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            trackedUnityObjects.Add(asset);
            return asset;
        }

        /// <summary> Creates one prefab asset owned by this scope. </summary>
        /// <param name="prefix"> The file-name prefix used to identify the test owner. </param>
        /// <param name="rootName"> The temporary prefab source root name. </param>
        /// <param name="childNames"> Optional direct-child names to attach under the source root. </param>
        /// <returns> The created project-relative prefab asset path. </returns>
        public string CreatePrefabAsset (
            string prefix,
            string rootName,
            params string[] childNames)
        {
            if (string.IsNullOrWhiteSpace(rootName))
            {
                throw new ArgumentException("Prefab root name must not be empty.", nameof(rootName));
            }

            var prefabPath = CreatePrefabPath(prefix);
            resetEditorSceneState = true;
            var sourceRoot = new GameObject(rootName);
            try
            {
                for (var i = 0; i < childNames.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(childNames[i]))
                    {
                        continue;
                    }

                    var child = new GameObject(childNames[i]);
                    child.transform.SetParent(sourceRoot.transform, worldPositionStays: false);
                }

                var prefabAsset = PrefabUtility.SaveAsPrefabAsset(sourceRoot, prefabPath);
                if (prefabAsset == null)
                {
                    throw new InvalidOperationException($"Prefab asset could not be created: {prefabPath}");
                }

                AssetDatabase.SaveAssets();
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceRoot);
            }
        }

        /// <summary> Loads editable prefab contents owned by this scope. </summary>
        /// <param name="prefabPath"> The project-relative prefab asset path. </param>
        /// <returns> The loaded prefab contents root. </returns>
        public GameObject LoadPrefabContents (string prefabPath)
        {
            var prefabContentsRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            trackedPrefabContentsRoots.Add(prefabContentsRoot);
            return prefabContentsRoot;
        }

        /// <summary> Unloads one editable prefab contents root that was loaded through this scope. </summary>
        /// <param name="prefabContentsRoot"> The loaded prefab contents root. </param>
        public void UnloadPrefabContents (GameObject prefabContentsRoot)
        {
            if (prefabContentsRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabContentsRoot));
            }

            for (var i = trackedPrefabContentsRoots.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(trackedPrefabContentsRoots[i], prefabContentsRoot))
                {
                    trackedPrefabContentsRoots.RemoveAt(i);
                    break;
                }
            }

            PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
        }

        /// <summary> Registers one temporary asset path to delete during cleanup. </summary>
        /// <param name="assetPath"> The project-relative asset path. </param>
        /// <returns> The current scope. </returns>
        public EditorTestScope TrackAsset (string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                assetPaths.Add(assetPath);
                if (assetPath.EndsWith(".unity", StringComparison.Ordinal))
                {
                    resetEditorSceneState = true;
                }
            }

            return this;
        }

        /// <summary> Registers one disposable resource to release before editor-state cleanup. </summary>
        /// <typeparam name="TDisposable"> The disposable type. </typeparam>
        /// <param name="disposable"> The tracked disposable resource. </param>
        /// <returns> The same <paramref name="disposable" /> instance. </returns>
        public TDisposable TrackDisposable<TDisposable> (TDisposable disposable)
            where TDisposable : IDisposable
        {
            trackedDisposables.Add(disposable);
            return disposable;
        }

        /// <summary> Requests prefab-stage cleanup during disposal. </summary>
        /// <returns> The current scope. </returns>
        public EditorTestScope EnablePrefabStageCleanup ()
        {
            closePrefabStage = true;
            return this;
        }

        /// <summary> Closes the current prefab stage, if any, and resets the editor scene back to the main stage. </summary>
        /// <returns> <see langword="true"/> if closing the prefab stage already reset the editor scene state. </returns>
        public bool CloseCurrentPrefabStageIfOpen ()
        {
            if (!closePrefabStage)
            {
                return false;
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                return false;
            }

            if (prefabStage.scene.IsValid())
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return true;
            }

            return false;
        }

        /// <summary> Requests editor scene reset during disposal even when no scene asset path is tracked. </summary>
        /// <returns> The current scope. </returns>
        public EditorTestScope EnableEditorSceneReset ()
        {
            resetEditorSceneState = true;
            return this;
        }

        /// <summary> Registers one Unity object for destruction during cleanup. </summary>
        /// <typeparam name="TUnityObject"> The Unity object type. </typeparam>
        /// <param name="unityObject"> The tracked Unity object. </param>
        /// <returns> The same <paramref name="unityObject" /> instance. </returns>
        public TUnityObject TrackUnityObject<TUnityObject> (TUnityObject unityObject)
            where TUnityObject : UnityEngine.Object
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            trackedUnityObjects.Add(unityObject);
            return unityObject;
        }

        /// <summary> Releases all tracked resources and resets editor state for the next test. </summary>
        public void Dispose ()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeTrackedResources();
            UnloadTrackedPrefabContentsRoots();
            var editorSceneWasReset = CloseCurrentPrefabStageIfOpen();
            ResetEditorSceneIfRequested(editorSceneWasReset);
            DeleteTrackedAssets();
            DestroyTrackedUnityObjects();
        }

        private void DisposeTrackedResources ()
        {
            for (var i = trackedDisposables.Count - 1; i >= 0; i--)
            {
                trackedDisposables[i].Dispose();
            }

            trackedDisposables.Clear();
        }

        private void UnloadTrackedPrefabContentsRoots ()
        {
            for (var i = trackedPrefabContentsRoots.Count - 1; i >= 0; i--)
            {
                if (trackedPrefabContentsRoots[i] != null)
                {
                    PrefabUtility.UnloadPrefabContents(trackedPrefabContentsRoots[i]);
                }
            }

            trackedPrefabContentsRoots.Clear();
        }

        private void ResetEditorSceneIfRequested (bool alreadyReset)
        {
            if (!resetEditorSceneState || alreadyReset)
            {
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private void DeleteTrackedAssets ()
        {
            foreach (var assetPath in assetPaths)
            {
                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    continue;
                }

                if (!AssetPathExists(assetPath))
                {
                    continue;
                }

                throw new InvalidOperationException($"Tracked test asset could not be deleted: {assetPath}");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            assetPaths.Clear();
        }

        private static bool AssetPathExists (string assetPath)
        {
            var projectRootPath = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
            }

            var relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.Combine(projectRootPath, relativePath);
            var metaPath = absolutePath + ".meta";

            return AssetDatabase.LoadMainAssetAtPath(assetPath) != null
                || File.Exists(absolutePath)
                || File.Exists(metaPath);
        }

        private void DestroyTrackedUnityObjects ()
        {
            for (var i = trackedUnityObjects.Count - 1; i >= 0; i--)
            {
                if (trackedUnityObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(trackedUnityObjects[i]);
                }
            }

            trackedUnityObjects.Clear();
        }
    }
}
