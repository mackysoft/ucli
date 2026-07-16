using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
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
        public static GameObjectDescriptionResult Build (
            GameObject root,
            int? depth)
        {
            return Build(root, depth, executionContext: null, includeTemporaryState: false);
        }

        /// <summary> Builds one structural description from the specified root GameObject and optional request-local state. </summary>
        /// <param name="root"> The root GameObject to describe. </param>
        /// <param name="depth"> The maximum child depth to include. <see langword="null" /> means unlimited depth. </param>
        /// <param name="executionContext"> The optional request execution context. </param>
        /// <param name="includeTemporaryState"> Whether request-local ensured components may augment the description. </param>
        /// <returns> The built GameObject description. </returns>
        public static GameObjectDescriptionResult Build (
            GameObject root,
            int? depth,
            OperationExecutionContext? executionContext,
            bool includeTemporaryState)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var maxDepth = depth ?? int.MaxValue;
            return Build(root.transform, currentDepth: 0, maxDepth, executionContext, includeTemporaryState);
        }

        /// <summary> Builds one description node from the specified transform. </summary>
        /// <param name="transform"> The transform to describe. </param>
        /// <param name="currentDepth"> The current recursion depth. </param>
        /// <param name="maxDepth"> The maximum recursion depth. </param>
        /// <returns> The built description node. </returns>
        private static GameObjectDescriptionResult Build (
            Transform transform,
            int currentDepth,
            int maxDepth,
            OperationExecutionContext? executionContext,
            bool includeTemporaryState)
        {
            var gameObject = transform.gameObject;
            var componentDescriptions = BuildComponentDescriptions(gameObject, executionContext, includeTemporaryState);

            var children = currentDepth >= maxDepth
                ? Array.Empty<GameObjectDescriptionResult>()
                : BuildChildren(transform, currentDepth, maxDepth, executionContext, includeTemporaryState);
            _ = ResolveReferenceResolver.TryCreateGameObjectGlobalObjectId(gameObject, executionContext, out var globalObjectId);
            return new GameObjectDescriptionResult(
                name: gameObject.name,
                globalObjectId: globalObjectId,
                components: componentDescriptions,
                children: children);
        }

        /// <summary> Builds child descriptions for the specified transform. </summary>
        /// <param name="transform"> The parent transform. </param>
        /// <param name="currentDepth"> The current recursion depth. </param>
        /// <param name="maxDepth"> The maximum recursion depth. </param>
        /// <returns> The built child description list. </returns>
        private static GameObjectDescriptionResult[] BuildChildren (
            Transform transform,
            int currentDepth,
            int maxDepth,
            OperationExecutionContext? executionContext,
            bool includeTemporaryState)
        {
            var childDescriptions = new GameObjectDescriptionResult[transform.childCount];
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                childDescriptions[childIndex] = Build(
                    transform.GetChild(childIndex),
                    currentDepth + 1,
                    maxDepth,
                    executionContext,
                    includeTemporaryState);
            }

            return childDescriptions;
        }

        private static GameObjectComponentDescriptionResult[] BuildComponentDescriptions (
            GameObject gameObject,
            OperationExecutionContext? executionContext,
            bool includeTemporaryState)
        {
            var components = gameObject.GetComponents<Component>();
            List<GameObjectComponentDescriptionResult>? ensuredComponentDescriptions = null;
            if (includeTemporaryState
                && executionContext != null
                && OperationResourceUtilities.TryResolveOwnerResource(
                    gameObject,
                    executionContext,
                    out var resource,
                    out _))
            {
                var targetTrackingKey = executionContext.CreateGameObjectTrackingKey(gameObject, resource);
                var ensuredComponents = new List<ComponentSandboxRegistry.EnsuredComponentState>();
                executionContext.CollectEnsuredComponentStates(targetTrackingKey, ensuredComponents);
                if (ensuredComponents.Count > 0)
                {
                    ensuredComponentDescriptions = new List<GameObjectComponentDescriptionResult>(ensuredComponents.Count);
                    for (var ensuredIndex = 0; ensuredIndex < ensuredComponents.Count; ensuredIndex++)
                    {
                        var ensuredComponent = ensuredComponents[ensuredIndex].Component;
                        if (ensuredComponent == null)
                        {
                            continue;
                        }

                        ensuredComponentDescriptions.Add(new GameObjectComponentDescriptionResult(
                            new UnityComponentTypeId(ensuredComponent.GetType().FullName!)));
                    }
                }
            }

            var componentDescriptionCount = components.Length + (ensuredComponentDescriptions != null ? ensuredComponentDescriptions.Count : 0);
            var componentDescriptions = new GameObjectComponentDescriptionResult[componentDescriptionCount];
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                componentDescriptions[componentIndex] = new GameObjectComponentDescriptionResult(
                    component != null
                        ? new UnityComponentTypeId(component.GetType().FullName!)
                        : null);
            }

            if (ensuredComponentDescriptions == null)
            {
                return componentDescriptions;
            }

            for (var ensuredIndex = 0; ensuredIndex < ensuredComponentDescriptions.Count; ensuredIndex++)
            {
                componentDescriptions[components.Length + ensuredIndex] = ensuredComponentDescriptions[ensuredIndex];
            }

            return componentDescriptions;
        }
    }
}
