using System;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves one hierarchy path to a unique GameObject under a prefab root. </summary>
    internal static class PrefabHierarchyPathResolver
    {
        /// <summary> Resolves a slash-separated hierarchy path whose first segment must match the supplied Prefab root. </summary>
        /// <param name="prefabRoot"> The loaded Prefab contents root to search under. </param>
        /// <param name="hierarchyPath"> The slash-separated hierarchy path, including the root GameObject name. </param>
        /// <param name="gameObject"> The resolved GameObject when the method returns <see langword="true" />; otherwise <see langword="null" />. </param>
        /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when the path resolves to exactly one GameObject; otherwise <see langword="false" />. </returns>
        public static bool TryResolve (
            GameObject prefabRoot,
            string hierarchyPath,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            if (prefabRoot == null)
            {
                errorMessage = "Prefab root is not available.";
                return false;
            }

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

            if (!string.Equals(prefabRoot.name, segments[0], StringComparison.Ordinal))
            {
                errorMessage = $"Hierarchy path was not found in prefab root '{prefabRoot.name}': {hierarchyPath}.";
                return false;
            }

            var current = prefabRoot.transform;
            for (var segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                var targetName = segments[segmentIndex];
                Transform? next = null;
                var matchCount = 0;
                for (var childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    var child = current.GetChild(childIndex);
                    if (!string.Equals(child.name, targetName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    next = child;
                    matchCount++;
                }

                if (matchCount == 0)
                {
                    errorMessage = $"Hierarchy path was not found in prefab root '{prefabRoot.name}': {hierarchyPath}.";
                    return false;
                }

                if (matchCount > 1)
                {
                    errorMessage = $"Hierarchy path resolved to multiple objects in prefab root '{prefabRoot.name}': {hierarchyPath}.";
                    return false;
                }

                current = next!;
            }

            gameObject = current.gameObject;
            errorMessage = string.Empty;
            return true;
        }
    }
}
