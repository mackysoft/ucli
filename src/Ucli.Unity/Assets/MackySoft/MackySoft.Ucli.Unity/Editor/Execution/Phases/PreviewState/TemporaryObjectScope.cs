using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
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

        /// <summary> Tracks one loaded temporary prefab-contents root for cleanup at request end. </summary>
        /// <param name="prefabPath"> The prefab asset path associated with the temporary contents root. </param>
        /// <param name="prefabContentsRoot"> The loaded temporary prefab-contents root. </param>
        public void TrackTemporaryPrefabContentsRoot (
            string prefabPath,
            GameObject prefabContentsRoot)
        {
            var stableReferenceIndex = new TemporaryPreviewStableReferenceIndex();
            TemporaryMirrorUtilities.RegisterPreviewPrefabStableReferencesBestEffort(prefabPath, prefabContentsRoot, stableReferenceIndex);
            TrackTemporaryPrefabContentsRoot(
                prefabPath,
                prefabContentsRoot,
                TemporaryPrefabCleanupKind.UnloadPrefabContents,
                mirrorMapping: null,
                stableReferenceIndex);
        }

        /// <summary> Gets one tracked temporary prefab root or mirrors the current opened Prefab Stage into request-local preview state. </summary>
        /// <param name="prefabPath"> The prefab asset path. Must not be <see langword="null" />, empty, or whitespace. </param>
        /// <param name="openedPrefabContentsRoot"> The opened Prefab Stage root to mirror. Must not be <see langword="null" />. </param>
        /// <param name="prefabContentsRoot"> The tracked or newly mirrored temporary prefab root when successful. </param>
        /// <param name="errorMessage"> The validation error message when the prefab mirror cannot be created. </param>
        /// <returns> <see langword="true" /> when request-local prefab contents are available; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="prefabPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="openedPrefabContentsRoot" /> is <see langword="null" />. </exception>
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
                var mirrorMapping = new TemporaryMirrorMapping();
                var stableReferenceIndex = new TemporaryPreviewStableReferenceIndex();
                if (!TemporaryMirrorUtilities.TryRegisterHierarchyMirror(openedPrefabContentsRoot, prefabContentsRoot, mirrorMapping, out errorMessage))
                {
                    UnityEngine.Object.DestroyImmediate(prefabContentsRoot);
                    EditorSceneManager.ClosePreviewScene(previewScene);
                    prefabContentsRoot = null;
                    return false;
                }

                if (!TemporaryMirrorUtilities.TryRebindMirroredLocalReferences(mirrorMapping, out errorMessage))
                {
                    UnityEngine.Object.DestroyImmediate(prefabContentsRoot);
                    EditorSceneManager.ClosePreviewScene(previewScene);
                    prefabContentsRoot = null;
                    return false;
                }

                TemporaryMirrorUtilities.RegisterMirroredHierarchyStableReferencesBestEffort(
                    openedPrefabContentsRoot,
                    prefabContentsRoot,
                    stableReferenceIndex);

                if (openedPrefabContentsRoot.scene.isDirty)
                {
                    EditorSceneManager.MarkSceneDirty(previewScene);
                }

                TrackTemporaryPrefabContentsRoot(
                    prefabPath,
                    prefabContentsRoot,
                    TemporaryPrefabCleanupKind.ClosePreviewScene,
                    mirrorMapping,
                    stableReferenceIndex);
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

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to get one tracked temporary prefab-contents root. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="prefabContentsRoot"> The tracked temporary prefab root when found. </param>
        /// <returns> <see langword="true" /> when a temporary prefab root is tracked for <paramref name="prefabPath" />; otherwise <see langword="false" />. </returns>
        public bool TryGetTemporaryPrefabContentsRoot (
            string prefabPath,
            [NotNullWhen(true)] out GameObject? prefabContentsRoot)
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

        /// <summary> Tries to resolve one tracked temporary prefab GameObject back to its prefab asset path. </summary>
        /// <param name="gameObject"> The candidate GameObject. Must not be <see langword="null" />. </param>
        /// <param name="prefabPath"> The tracked prefab asset path when found. </param>
        /// <returns> <see langword="true" /> when <paramref name="gameObject" /> belongs to tracked temporary prefab contents; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="gameObject" /> is <see langword="null" />. </exception>
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

        /// <summary> Tries to resolve one preview prefab object back to its mirrored live source object. </summary>
        /// <param name="prefabPath"> The prefab asset path that owns the temporary contents. </param>
        /// <param name="previewObject"> The preview object. Must not be <see langword="null" />. </param>
        /// <param name="sourceObject"> The mirrored live source object when found. </param>
        /// <returns> <see langword="true" /> when the preview object belongs to a mirrored opened Prefab Stage snapshot; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="previewObject" /> is <see langword="null" />. </exception>
        public bool TryResolveMirroredSourceObject (
            string prefabPath,
            UnityEngine.Object previewObject,
            out UnityEngine.Object? sourceObject)
        {
            sourceObject = null;
            if (previewObject == null)
            {
                throw new ArgumentNullException(nameof(previewObject));
            }

            if (!temporaryPrefabContentsRootsByPath.TryGetValue(prefabPath, out var trackedRoot))
            {
                return false;
            }

            if (trackedRoot.MirrorMapping == null)
            {
                return false;
            }

            return trackedRoot.MirrorMapping.TryGetSourceObject(previewObject, out sourceObject);
        }

        /// <summary> Tries to resolve one mirrored live prefab object to its request-local preview counterpart. </summary>
        /// <param name="prefabPath"> The prefab asset path that owns the temporary contents. </param>
        /// <param name="sourceObject"> The mirrored live source object. Must not be <see langword="null" />. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the mirrored live object has a preview counterpart; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="sourceObject" /> is <see langword="null" />. </exception>
        public bool TryResolvePreviewObjectFromMirroredSourceObject (
            string prefabPath,
            UnityEngine.Object sourceObject,
            out UnityEngine.Object? previewObject)
        {
            previewObject = null;
            if (sourceObject == null)
            {
                throw new ArgumentNullException(nameof(sourceObject));
            }

            if (!temporaryPrefabContentsRootsByPath.TryGetValue(prefabPath, out var trackedRoot))
            {
                return false;
            }

            if (trackedRoot.MirrorMapping == null)
            {
                return false;
            }

            return trackedRoot.MirrorMapping.TryGetPreviewObject(sourceObject, out previewObject);
        }

        /// <summary> Tries to resolve one preview prefab object to its stable source identity. </summary>
        /// <param name="prefabPath"> The prefab asset path that owns the temporary contents. </param>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="globalObjectId"> The stable source identity when found. </param>
        /// <returns> <see langword="true" /> when the preview object has one explicit stable-reference mapping; otherwise <see langword="false" />. </returns>
        public bool TryResolveGlobalObjectIdFromPreviewObject (
            string prefabPath,
            UnityEngine.Object previewObject,
            [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
        {
            globalObjectId = null;
            if (!temporaryPrefabContentsRootsByPath.TryGetValue(prefabPath, out var trackedRoot))
            {
                return false;
            }

            return trackedRoot.StableReferenceIndex.TryGetGlobalObjectId(previewObject, out globalObjectId);
        }

        /// <summary> Tries to resolve one stable source identity to its preview prefab object in the specified temporary contents root. </summary>
        /// <param name="prefabPath"> The prefab asset path that owns the temporary contents. </param>
        /// <param name="globalObjectId"> The stable source identity. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the stable reference maps into the specified temporary contents; otherwise <see langword="false" />. </returns>
        public bool TryResolvePreviewObjectFromGlobalObjectId (
            string prefabPath,
            UnityGlobalObjectId globalObjectId,
            out UnityEngine.Object? previewObject)
        {
            previewObject = null;
            if (!temporaryPrefabContentsRootsByPath.TryGetValue(prefabPath, out var trackedRoot))
            {
                return false;
            }

            return trackedRoot.StableReferenceIndex.TryGetPreviewObject(globalObjectId, out previewObject);
        }

        /// <summary> Tries to resolve one stable source identity to any tracked preview prefab object. </summary>
        /// <param name="globalObjectId"> The stable source identity. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the stable reference maps into one tracked temporary prefab contents root; otherwise <see langword="false" />. </returns>
        public bool TryResolvePreviewObjectFromGlobalObjectId (
            UnityGlobalObjectId globalObjectId,
            out UnityEngine.Object? previewObject)
        {
            previewObject = null;
            foreach (var pair in temporaryPrefabContentsRootsByPath)
            {
                if (pair.Value.StableReferenceIndex.TryGetPreviewObject(globalObjectId, out previewObject))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary> Tracks one temporary Unity object for destruction at request end. </summary>
        /// <param name="unityObject"> The temporary Unity object. Must not be <see langword="null" />. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is <see langword="null" />. </exception>
        public void TrackTemporaryObject (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            temporaryObjects.Add(unityObject);
        }

        /// <summary> Determines whether one Unity object is tracked as temporary request-local state. </summary>
        /// <param name="unityObject"> The candidate Unity object. Must not be <see langword="null" />. </param>
        /// <returns> <see langword="true" /> when the object is tracked for temporary cleanup; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is <see langword="null" />. </exception>
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

        /// <summary> Releases one tracked temporary prefab-contents root and performs the configured cleanup. </summary>
        /// <param name="prefabPath"> The prefab asset path whose temporary contents should be released. </param>
        /// <returns> <see langword="true" /> when tracked temporary contents were released; otherwise <see langword="false" />. </returns>
        public bool ReleaseTemporaryPrefabContentsRoot (string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                return false;
            }

            if (!temporaryPrefabContentsRootsByPath.TryGetValue(prefabPath, out var trackedRoot))
            {
                return false;
            }

            temporaryPrefabContentsRootsByPath.Remove(prefabPath);
            CleanupTemporaryPrefabContentsRoot(trackedRoot);
            return true;
        }

        /// <summary> Releases every tracked temporary prefab root and destroys every tracked temporary Unity object. </summary>
        public void Cleanup ()
        {
            foreach (var pair in temporaryPrefabContentsRootsByPath)
            {
                CleanupTemporaryPrefabContentsRoot(pair.Value);
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
            TemporaryPrefabCleanupKind cleanupKind,
            TemporaryMirrorMapping? mirrorMapping,
            TemporaryPreviewStableReferenceIndex stableReferenceIndex)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path must not be null, empty, or whitespace.", nameof(prefabPath));
            }

            if (prefabContentsRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabContentsRoot));
            }

            if (stableReferenceIndex == null)
            {
                throw new ArgumentNullException(nameof(stableReferenceIndex));
            }

            temporaryPrefabContentsRootsByPath[prefabPath] =
                new TemporaryPrefabContentsRoot(prefabContentsRoot, cleanupKind, mirrorMapping, stableReferenceIndex);
        }

        private static void CleanupTemporaryPrefabContentsRoot (TemporaryPrefabContentsRoot trackedRoot)
        {
            var prefabContentsRoot = trackedRoot.PrefabContentsRoot;
            if (prefabContentsRoot == null)
            {
                return;
            }

            switch (trackedRoot.CleanupKind)
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

        private readonly struct TemporaryPrefabContentsRoot
        {
            public TemporaryPrefabContentsRoot (
                GameObject prefabContentsRoot,
                TemporaryPrefabCleanupKind cleanupKind,
                TemporaryMirrorMapping? mirrorMapping,
                TemporaryPreviewStableReferenceIndex stableReferenceIndex)
            {
                PrefabContentsRoot = prefabContentsRoot;
                CleanupKind = cleanupKind;
                MirrorMapping = mirrorMapping;
                StableReferenceIndex = stableReferenceIndex;
            }

            public GameObject PrefabContentsRoot { get; }

            public TemporaryPrefabCleanupKind CleanupKind { get; }

            public TemporaryMirrorMapping? MirrorMapping { get; }

            public TemporaryPreviewStableReferenceIndex StableReferenceIndex { get; }
        }

        private enum TemporaryPrefabCleanupKind
        {
            UnloadPrefabContents = 0,
            ClosePreviewScene = 1,
        }
    }
}
