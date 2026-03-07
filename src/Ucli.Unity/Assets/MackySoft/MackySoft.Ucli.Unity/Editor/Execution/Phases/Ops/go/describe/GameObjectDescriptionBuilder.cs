using System;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Builds structural GameObject descriptions for <c>ucli.go.describe</c>. </summary>
    internal static class GameObjectDescriptionBuilder
    {
        /// <summary> Builds one structural description from the specified root GameObject. </summary>
        /// <param name="root"> The root GameObject to describe. </param>
        /// <param name="depth"> The maximum child depth to include. <see langword="null" /> means unlimited depth. </param>
        /// <returns> The built GameObject description. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="root" /> is <see langword="null" />. </exception>
        public static GameObjectDescription Build (
            GameObject root,
            int? depth)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var maxDepth = depth ?? int.MaxValue;
            return Build(root.transform, currentDepth: 0, maxDepth);
        }

        /// <summary> Builds one description node from the specified transform. </summary>
        /// <param name="transform"> The transform to describe. </param>
        /// <param name="currentDepth"> The current recursion depth. </param>
        /// <param name="maxDepth"> The maximum recursion depth. </param>
        /// <returns> The built description node. </returns>
        private static GameObjectDescription Build (
            Transform transform,
            int currentDepth,
            int maxDepth)
        {
            var gameObject = transform.gameObject;
            var components = gameObject.GetComponents<Component>();
            var componentDescriptions = new GameObjectComponentDescription[components.Length];
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                componentDescriptions[componentIndex] = new GameObjectComponentDescription(component != null ? component.GetType().FullName : null);
            }

            var children = currentDepth >= maxDepth
                ? Array.Empty<GameObjectDescription>()
                : BuildChildren(transform, currentDepth, maxDepth);
            return new GameObjectDescription(
                Name: gameObject.name,
                GlobalObjectId: GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString(),
                Components: componentDescriptions,
                Children: children);
        }

        /// <summary> Builds child descriptions for the specified transform. </summary>
        /// <param name="transform"> The parent transform. </param>
        /// <param name="currentDepth"> The current recursion depth. </param>
        /// <param name="maxDepth"> The maximum recursion depth. </param>
        /// <returns> The built child description list. </returns>
        private static GameObjectDescription[] BuildChildren (
            Transform transform,
            int currentDepth,
            int maxDepth)
        {
            var childDescriptions = new GameObjectDescription[transform.childCount];
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                childDescriptions[childIndex] = Build(transform.GetChild(childIndex), currentDepth + 1, maxDepth);
            }

            return childDescriptions;
        }
    }
}