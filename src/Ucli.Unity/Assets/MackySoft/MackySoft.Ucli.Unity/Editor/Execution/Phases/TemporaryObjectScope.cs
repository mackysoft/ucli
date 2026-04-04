using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks temporary Unity objects and prefab contents roots that must be cleaned at request end. </summary>
    internal sealed class TemporaryObjectScope
    {
        private readonly Dictionary<string, GameObject> temporaryPrefabContentsRootsByPath =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);

        private readonly List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();

        public void TrackTemporaryPrefabContentsRoot (
            string prefabPath,
            GameObject prefabContentsRoot)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path must not be null, empty, or whitespace.", nameof(prefabPath));
            }

            if (prefabContentsRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabContentsRoot));
            }

            temporaryPrefabContentsRootsByPath[prefabPath] = prefabContentsRoot;
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

            if (value == null)
            {
                temporaryPrefabContentsRootsByPath.Remove(prefabPath);
                return false;
            }

            prefabContentsRoot = value;
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
                var prefabContentsRoot = pair.Value;
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
                var prefabContentsRoot = pair.Value;
                if (prefabContentsRoot != null)
                {
                    UnityEditor.PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
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
    }
}
