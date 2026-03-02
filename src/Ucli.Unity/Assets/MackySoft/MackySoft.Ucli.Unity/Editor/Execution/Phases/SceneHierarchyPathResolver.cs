using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves one hierarchy path to a unique GameObject in a loaded scene. </summary>
    internal static class SceneHierarchyPathResolver
    {
        /// <summary> Tries to resolve one unique GameObject from a loaded scene path and hierarchy path. </summary>
        /// <param name="scenePath"> The scene path value. </param>
        /// <param name="hierarchyPath"> The hierarchy path value. </param>
        /// <param name="gameObject"> The resolved GameObject when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when path resolves uniquely; otherwise <see langword="false" />. </returns>
        public static bool TryResolveLoadedSceneObject (
            string scenePath,
            string hierarchyPath,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                errorMessage =
                    $"Scene is not loaded: {scenePath}. Resolve with '{ResolveSelectorPropertyNames.Scene}' + '{ResolveSelectorPropertyNames.HierarchyPath}' requires the scene to be opened first.";
                return false;
            }

            return TryResolveUniqueGameObject(scene, hierarchyPath, out gameObject, out errorMessage);
        }

        /// <summary> Resolves one unique GameObject under the specified scene and hierarchy path. </summary>
        /// <param name="scene"> The loaded scene. </param>
        /// <param name="hierarchyPath"> The hierarchy path value. </param>
        /// <param name="gameObject"> The resolved GameObject when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when path resolves uniquely; otherwise <see langword="false" />. </returns>
        private static bool TryResolveUniqueGameObject (
            Scene scene,
            string hierarchyPath,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            var segments = hierarchyPath.Split('/');
            if (segments.Length == 0)
            {
                errorMessage = $"Hierarchy path is invalid: {hierarchyPath}.";
                return false;
            }

            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0)
                {
                    errorMessage = $"Hierarchy path contains empty segment: {hierarchyPath}.";
                    return false;
                }
            }

            var current = new List<Transform>();
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                if (string.Equals(root.name, segments[0], StringComparison.Ordinal))
                {
                    current.Add(root.transform);
                }
            }

            if (current.Count == 0)
            {
                errorMessage = $"Hierarchy path was not found in loaded scene '{scene.path}': {hierarchyPath}.";
                return false;
            }

            for (var segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                var targetName = segments[segmentIndex];
                var next = new List<Transform>();
                for (var parentIndex = 0; parentIndex < current.Count; parentIndex++)
                {
                    var parent = current[parentIndex];
                    for (var childIndex = 0; childIndex < parent.childCount; childIndex++)
                    {
                        var child = parent.GetChild(childIndex);
                        if (string.Equals(child.name, targetName, StringComparison.Ordinal))
                        {
                            next.Add(child);
                        }
                    }
                }

                if (next.Count == 0)
                {
                    errorMessage = $"Hierarchy path was not found in loaded scene '{scene.path}': {hierarchyPath}.";
                    return false;
                }

                current = next;
            }

            if (current.Count > 1)
            {
                errorMessage = $"Hierarchy path resolved to multiple objects in loaded scene '{scene.path}': {hierarchyPath}.";
                return false;
            }

            gameObject = current[0].gameObject;
            errorMessage = string.Empty;
            return true;
        }
    }
}
