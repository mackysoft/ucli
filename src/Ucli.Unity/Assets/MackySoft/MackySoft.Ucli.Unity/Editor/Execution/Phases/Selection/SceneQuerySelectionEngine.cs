using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.SceneInspection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary>
    /// <para> Resolves scene-scoped query selections into deterministic query matches. </para>
    /// <para> The produced match list follows scene hierarchy traversal order and is deduplicated by canonical selector identity. </para>
    /// </summary>
    internal static class SceneQuerySelectionEngine
    {
        /// <summary>
        /// Executes one runtime scene query against request-local plan state or current loaded state.
        /// </summary>
        /// <param name="scenePath"> The queried scene asset path. </param>
        /// <param name="queryArguments"> The parsed scene-query arguments. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to observe request-local preview-scene state during plan execution without creating new tracked preview state. </param>
        /// <param name="matches"> The deduplicated matches in scene hierarchy traversal order when the query succeeds. </param>
        /// <param name="diagnostics"> The non-fatal diagnostics emitted while collecting matches. </param>
        /// <param name="errorMessage"> The validation or query error message when the query fails. </param>
        /// <returns> <see langword="true" /> when the scene can be queried successfully; otherwise <see langword="false" />. </returns>
        public static bool TryQueryRuntime (
            string scenePath,
            QueryArguments queryArguments,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out List<QueryMatch> matches,
            out IReadOnlyList<OperationDiagnostic> diagnostics,
            out string errorMessage)
        {
            matches = new List<QueryMatch>();
            diagnostics = Array.Empty<OperationDiagnostic>();
            errorMessage = string.Empty;
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            SceneSourceLease sceneLease;
            var acquired = allowTemporaryState
                ? SceneSourceResolver.TryAcquireTrackedTemporaryOrLoadedOrPersistedPreview(scenePath, executionContext, out sceneLease, out errorMessage)
                : SceneReadSourceResolver.TryAcquireLoadedOrPersistedPreview(scenePath, out sceneLease, out errorMessage);
            if (!acquired)
            {
                return false;
            }

            using (sceneLease)
            {
                return CollectMatches(scenePath, sceneLease.Scene, queryArguments, executionContext, allowTemporaryState, out matches, out diagnostics, out errorMessage);
            }
        }

        private static bool CollectMatches (
            string scenePath,
            Scene scene,
            QueryArguments queryArguments,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out List<QueryMatch> matches,
            out IReadOnlyList<OperationDiagnostic> diagnostics,
            out string errorMessage)
        {
            var collectedDiagnostics = new List<OperationDiagnostic>();
            var matchesInTraversalOrder = new List<QueryMatch>();
            var seenCanonicalKeys = new HashSet<string>(StringComparer.Ordinal);
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (!TryCollectMatchesRecursive(
                    scenePath,
                    roots[i].transform,
                    queryArguments,
                    executionContext,
                    allowTemporaryState,
                    matchesInTraversalOrder,
                    seenCanonicalKeys,
                    collectedDiagnostics,
                    out errorMessage))
                {
                    matches = new List<QueryMatch>();
                    diagnostics = collectedDiagnostics.ToArray();
                    return false;
                }
            }

            matches = matchesInTraversalOrder;
            diagnostics = collectedDiagnostics.ToArray();
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryCollectMatchesRecursive (
            string scenePath,
            Transform transform,
            QueryArguments queryArguments,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            List<QueryMatch> matchesInTraversalOrder,
            HashSet<string> seenCanonicalKeys,
            List<OperationDiagnostic> diagnostics,
            out string errorMessage)
        {
            if (transform.name.Contains("/", StringComparison.Ordinal))
            {
                // NOTE:
                // hierarchyPath selectors use '/' as a structural delimiter, so a GameObject name that contains '/'
                // cannot be represented or re-resolved later. Skip the entire subtree instead of failing unrelated queries.
                diagnostics.Add(CreateHierarchyPathUnrepresentableObjectsDiagnostic());
                errorMessage = string.Empty;
                return true;
            }

            var hierarchyPath = CreateHierarchyPath(transform);
            if (string.IsNullOrEmpty(hierarchyPath))
            {
                errorMessage = "Hierarchy path could not be created.";
                return false;
            }

            if (MatchesPathPrefix(hierarchyPath, queryArguments.PathPrefix))
            {
                if (queryArguments.ComponentTypeId == null)
                {
                    if (!RegisterMatch(
                            matchesInTraversalOrder,
                            seenCanonicalKeys,
                            new QueryMatch(
                                QueryTargetKind.GameObject,
                                hierarchyPath,
                                null,
                                CreateCanonicalKey(scenePath, QueryTargetKind.GameObject, hierarchyPath, null)),
                            out errorMessage))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!TryCreateComponentMatch(
                            transform.gameObject,
                            hierarchyPath,
                            scenePath,
                            queryArguments.ComponentTypeId,
                            queryArguments.ComponentRuntimeType!,
                            executionContext,
                            allowTemporaryState,
                            out var componentMatch,
                            out errorMessage))
                    {
                        return false;
                    }

                    if (componentMatch != null)
                    {
                        if (!RegisterMatch(matchesInTraversalOrder, seenCanonicalKeys, componentMatch.Value, out errorMessage))
                        {
                            return false;
                        }
                    }
                }
            }

            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                if (!TryCollectMatchesRecursive(
                    scenePath,
                    transform.GetChild(childIndex),
                    queryArguments,
                    executionContext,
                    allowTemporaryState,
                    matchesInTraversalOrder,
                    seenCanonicalKeys,
                    diagnostics,
                    out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static OperationDiagnostic CreateHierarchyPathUnrepresentableObjectsDiagnostic ()
        {
            return new OperationDiagnostic(
                Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                Severity: UcliDiagnosticSeverity.Warning,
                CoverageImpact: IpcExecuteDiagnosticCoverageImpact.Partial,
                Message: "Scene query skipped GameObjects whose names contain '/' because hierarchyPath cannot represent them.");
        }

        private static bool TryCreateComponentMatch (
            GameObject gameObject,
            string hierarchyPath,
            string scenePath,
            UnityComponentTypeId componentTypeId,
            Type componentRuntimeType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out QueryMatch? match,
            out string errorMessage)
        {
            match = null;
            if (!ComponentOperationUtilities.TryResolveComponentSelector(
                    gameObject,
                    componentRuntimeType,
                    executionContext,
                    allowTemporaryState,
                    out var resolution,
                    out errorMessage))
            {
                return false;
            }

            if (resolution.MatchCount == 0)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (resolution.MatchCount > 1)
            {
                errorMessage = $"Scene query target '{hierarchyPath}' resolved multiple components of type '{componentTypeId.Value}'.";
                return false;
            }

            match = new QueryMatch(
                QueryTargetKind.Component,
                hierarchyPath,
                componentTypeId,
                CreateCanonicalKey(scenePath, QueryTargetKind.Component, hierarchyPath, componentTypeId));
            errorMessage = string.Empty;
            return true;
        }

        private static bool RegisterMatch (
            ICollection<QueryMatch> matchesInTraversalOrder,
            ISet<string> seenCanonicalKeys,
            QueryMatch match,
            out string errorMessage)
        {
            if (!seenCanonicalKeys.Add(match.CanonicalKey))
            {
                errorMessage = $"Scene query resolved duplicate canonical target at '{match.HierarchyPath}'.";
                return false;
            }

            matchesInTraversalOrder.Add(match);
            errorMessage = string.Empty;
            return true;
        }

        private static bool MatchesPathPrefix (
            string hierarchyPath,
            string? pathPrefix)
        {
            if (string.IsNullOrWhiteSpace(pathPrefix))
            {
                return true;
            }

            if (string.Equals(hierarchyPath, pathPrefix, StringComparison.Ordinal))
            {
                return true;
            }

            return hierarchyPath.StartsWith(pathPrefix + "/", StringComparison.Ordinal);
        }

        private static string CreateHierarchyPath (Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string CreateCanonicalKey (
            string scenePath,
            QueryTargetKind targetKind,
            string hierarchyPath,
            UnityComponentTypeId? componentType)
        {
            return "scene"
                   + "\u001f"
                   + scenePath
                   + "\u001f"
                   + targetKind.ToString()
                   + "\u001f"
                   + hierarchyPath
                   + "\u001f"
                   + (componentType?.Value ?? string.Empty);
        }

        /// <summary> Defines match target categories produced by scene query resolution. </summary>
        internal enum QueryTargetKind
        {
            /// <summary> A GameObject match. </summary>
            GameObject,

            /// <summary> A component match. </summary>
            Component,
        }

        /// <summary>
        /// Represents the parsed arguments for one scene query.
        /// </summary>
        /// <param name="PathPrefix"> The optional hierarchy-path prefix filter. <see langword="null" /> matches the entire scene. </param>
        /// <param name="ComponentTypeId"> The optional component type identifier. <see langword="null" /> selects GameObjects instead of components. </param>
        /// <param name="ComponentRuntimeType"> The runtime type resolved from <paramref name="ComponentTypeId" /> before scene traversal starts. </param>
        internal readonly struct QueryArguments
        {
            public QueryArguments (
                string? pathPrefix,
                UnityComponentTypeId? componentTypeId,
                Type? componentRuntimeType)
            {
                if ((componentTypeId == null) != (componentRuntimeType == null))
                {
                    throw new ArgumentException("Component type id and resolved runtime type must either both be specified or both be null.");
                }

                if (componentRuntimeType != null
                    && (!typeof(Component).IsAssignableFrom(componentRuntimeType)
                        || !OperationRuntimeTypeResolver.IsConcreteRuntimeType(componentRuntimeType)))
                {
                    throw new ArgumentException("Resolved component runtime type must be a concrete Unity Component type.", nameof(componentRuntimeType));
                }

                PathPrefix = pathPrefix;
                ComponentTypeId = componentTypeId;
                ComponentRuntimeType = componentRuntimeType;
            }

            public string? PathPrefix { get; }

            public UnityComponentTypeId? ComponentTypeId { get; }

            public Type? ComponentRuntimeType { get; }
        }

        /// <summary>
        /// Represents one deterministic scene-query match.
        /// </summary>
        /// <param name="TargetKind"> The compiled target category produced by the match. </param>
        /// <param name="HierarchyPath"> The matched hierarchy path relative to the scene root. </param>
        /// <param name="ComponentType"> The matched component type. <see langword="null" /> for GameObject matches. </param>
        /// <param name="CanonicalKey"> The canonical identity key used for deduplication. </param>
        internal readonly struct QueryMatch
        {
            public QueryMatch (
                QueryTargetKind targetKind,
                string hierarchyPath,
                UnityComponentTypeId? componentType,
                string canonicalKey)
            {
                TargetKind = targetKind;
                HierarchyPath = hierarchyPath;
                ComponentType = componentType;
                CanonicalKey = canonicalKey;
            }

            public QueryTargetKind TargetKind { get; }

            public string HierarchyPath { get; }

            public UnityComponentTypeId? ComponentType { get; }

            public string CanonicalKey { get; }
        }
    }
}
