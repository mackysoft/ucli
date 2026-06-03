using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.SceneInspection
{
    /// <summary> Builds deterministic scene-tree-lite node snapshots from one loaded scene. </summary>
    internal static class SceneTreeNodeSnapshotBuilder
    {
        /// <summary> Builds root-node snapshots for the specified scene. </summary>
        /// <param name="scene"> The loaded scene. </param>
        /// <param name="depth"> The optional depth limit. <see langword="null" /> means unlimited depth. </param>
        /// <param name="globalObjectIdResolver"> Optional request-local stable-reference resolver. </param>
        /// <returns> The built root-node snapshots in hierarchy order. </returns>
        public static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> BuildRoots (
            Scene scene,
            int? depth,
            Func<GameObject, string?>? globalObjectIdResolver = null)
        {
            var maxDepth = depth ?? int.MaxValue;
            var roots = scene.GetRootGameObjects();
            var rootNodes = new IndexSceneTreeLiteNodeJsonContract[roots.Length];
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                rootNodes[rootIndex] = BuildNode(roots[rootIndex], currentDepth: 0, maxDepth, globalObjectIdResolver);
            }

            return rootNodes;
        }

        private static IndexSceneTreeLiteNodeJsonContract BuildNode (
            GameObject gameObject,
            int currentDepth,
            int maxDepth,
            Func<GameObject, string?>? globalObjectIdResolver)
        {
            var children = currentDepth >= maxDepth
                ? System.Array.Empty<IndexSceneTreeLiteNodeJsonContract>()
                : BuildChildren(gameObject.transform, currentDepth + 1, maxDepth, globalObjectIdResolver);
            var childrenState = currentDepth >= maxDepth && gameObject.transform.childCount > 0
                ? IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth
                : IndexSceneTreeLiteNodeChildrenStateValues.Complete;
            var globalObjectId = ResolveGlobalObjectId(gameObject, globalObjectIdResolver);
            return new IndexSceneTreeLiteNodeJsonContract(
                name: gameObject.name,
                globalObjectId: globalObjectId,
                children: children,
                childrenState: childrenState);
        }

        private static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> BuildChildren (
            Transform transform,
            int childDepth,
            int maxDepth,
            Func<GameObject, string?>? globalObjectIdResolver)
        {
            if (transform.childCount == 0)
            {
                return System.Array.Empty<IndexSceneTreeLiteNodeJsonContract>();
            }

            var children = new IndexSceneTreeLiteNodeJsonContract[transform.childCount];
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                children[childIndex] = BuildNode(transform.GetChild(childIndex).gameObject, childDepth, maxDepth, globalObjectIdResolver);
            }

            return children;
        }

        private static string ResolveGlobalObjectId (
            GameObject gameObject,
            Func<GameObject, string?>? globalObjectIdResolver)
        {
            if (globalObjectIdResolver != null)
            {
                var resolvedGlobalObjectId = globalObjectIdResolver(gameObject);
                if (!string.IsNullOrWhiteSpace(resolvedGlobalObjectId))
                {
                    return resolvedGlobalObjectId;
                }
            }

            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString();
            return GlobalObjectId.TryParse(globalObjectId, out var parsedGlobalObjectId)
                ? parsedGlobalObjectId.ToString()
                : string.Empty;
        }
    }
}
