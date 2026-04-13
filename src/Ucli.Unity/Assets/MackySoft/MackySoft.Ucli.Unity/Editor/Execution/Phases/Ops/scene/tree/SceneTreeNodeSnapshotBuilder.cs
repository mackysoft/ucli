using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Index;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Builds deterministic scene-tree-lite node snapshots from one loaded scene. </summary>
    internal static class SceneTreeNodeSnapshotBuilder
    {
        /// <summary> Builds root-node snapshots for the specified scene. </summary>
        /// <param name="scene"> The loaded scene. </param>
        /// <param name="depth"> The optional depth limit. <see langword="null" /> means unlimited depth. </param>
        /// <param name="executionContext"> The execution context when temporary preview references should be considered. </param>
        /// <returns> The built root-node snapshots in hierarchy order. </returns>
        public static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> BuildRoots (
            Scene scene,
            int? depth,
            OperationExecutionContext? executionContext)
        {
            var maxDepth = depth ?? int.MaxValue;
            var roots = scene.GetRootGameObjects();
            var rootNodes = new IndexSceneTreeLiteNodeJsonContract[roots.Length];
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                rootNodes[rootIndex] = BuildNode(roots[rootIndex], currentDepth: 0, maxDepth, executionContext);
            }

            return rootNodes;
        }

        private static IndexSceneTreeLiteNodeJsonContract BuildNode (
            GameObject gameObject,
            int currentDepth,
            int maxDepth,
            OperationExecutionContext? executionContext)
        {
            var children = currentDepth >= maxDepth
                ? System.Array.Empty<IndexSceneTreeLiteNodeJsonContract>()
                : BuildChildren(gameObject.transform, currentDepth + 1, maxDepth, executionContext);
            var globalObjectId = ResolveReferenceResolver.TryCreateGameObjectResolvedReference(gameObject, executionContext, out var resolvedReference)
                ? resolvedReference!.GlobalObjectId
                : string.Empty;
            return new IndexSceneTreeLiteNodeJsonContract(
                Name: gameObject.name,
                GlobalObjectId: globalObjectId,
                Children: children);
        }

        private static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> BuildChildren (
            Transform transform,
            int childDepth,
            int maxDepth,
            OperationExecutionContext? executionContext)
        {
            if (transform.childCount == 0)
            {
                return System.Array.Empty<IndexSceneTreeLiteNodeJsonContract>();
            }

            var children = new IndexSceneTreeLiteNodeJsonContract[transform.childCount];
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                children[childIndex] = BuildNode(transform.GetChild(childIndex).gameObject, childDepth, maxDepth, executionContext);
            }

            return children;
        }
    }
}
