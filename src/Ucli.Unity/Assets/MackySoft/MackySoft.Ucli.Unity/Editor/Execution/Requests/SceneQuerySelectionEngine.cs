using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Unity.Execution.Phases;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary>
    /// <para> Resolves scene-scoped query selections into deterministic query matches. </para>
    /// <para> The produced match list is deduplicated by canonical selector identity and sorted in ascending canonical-key order. </para>
    /// </summary>
    internal static class SceneQuerySelectionEngine
    {
        /// <summary>
        /// Resolves one scene-scoped <c>select.from</c> declaration for edit compilation against request-local runtime state.
        /// </summary>
        /// <param name="step"> The validated edit-step contract. Only scene context is supported. </param>
        /// <param name="executionContext"> The current request execution context used to observe earlier plan-time mutations. </param>
        /// <param name="matches"> The deduplicated query matches in deterministic execution order when resolution succeeds. </param>
        /// <param name="errorMessage"> The validation or query error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the edit selection can be resolved into deterministic matches; otherwise <see langword="false" />. </returns>
        public static bool TryResolveForEditRuntime (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            out List<QueryMatch> matches,
            out string errorMessage)
        {
            matches = new List<QueryMatch>();
            errorMessage = string.Empty;
            if (step.Context.Kind != IpcEditStepContract.ContextKind.Scene)
            {
                errorMessage = "Edit step query selection is supported only for scene context.";
                return false;
            }

            if (!IpcSceneQueryArgsContractReader.TryReadForEditSelection(step.Selection.SourceArgs, out var parsedArgs, out errorMessage))
            {
                return false;
            }

            var queryArguments = CreateQueryArguments(parsedArgs);
            if (!TryQueryRuntime(step.Context.Path!, queryArguments, executionContext, allowTemporaryState: true, out var resolvedMatches, out errorMessage))
            {
                return false;
            }

            matches = resolvedMatches;
            return true;
        }

        /// <summary>
        /// Parses one external scene-query argument object and verifies that it matches the expected scene context.
        /// </summary>
        /// <param name="scenePath"> The required scene asset path for the enclosing execution context. </param>
        /// <param name="args"> The source JSON argument object. </param>
        /// <param name="queryArguments"> The parsed scene-query arguments when parsing succeeds. </param>
        /// <param name="errorMessage"> The validation error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the argument object is valid and targets <paramref name="scenePath" />; otherwise <see langword="false" />. </returns>
        public static bool TryParseExternalArgs (
            string scenePath,
            System.Text.Json.JsonElement args,
            out QueryArguments queryArguments,
            out string errorMessage)
        {
            queryArguments = default;
            errorMessage = string.Empty;
            if (!IpcSceneQueryArgsContractReader.TryReadForOperation(args, out var parsedArgs, out errorMessage))
            {
                return false;
            }

            var parsedScenePath = parsedArgs.ScenePath;
            if (!string.Equals(parsedScenePath, scenePath, StringComparison.Ordinal))
            {
                errorMessage = $"Operation 'args.scene' must match the requested scene context: {scenePath}.";
                return false;
            }

            queryArguments = CreateQueryArguments(parsedArgs);
            return true;
        }

        /// <summary>
        /// Parses one public <c>ucli.scene.query</c> argument object.
        /// </summary>
        /// <param name="args"> The source JSON argument object. </param>
        /// <param name="scenePath"> The parsed scene asset path when parsing succeeds. </param>
        /// <param name="queryArguments"> The parsed scene-query arguments when parsing succeeds. </param>
        /// <param name="errorMessage"> The validation error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the argument object is valid; otherwise <see langword="false" />. </returns>
        public static bool TryParseOpArgs (
            System.Text.Json.JsonElement args,
            out string scenePath,
            out QueryArguments queryArguments,
            out string errorMessage)
        {
            queryArguments = default;
            if (!IpcSceneQueryArgsContractReader.TryReadForOperation(args, out var parsedArgs, out errorMessage))
            {
                scenePath = string.Empty;
                return false;
            }

            scenePath = parsedArgs.ScenePath!;
            queryArguments = CreateQueryArguments(parsedArgs);
            return true;
        }

        /// <summary>
        /// Executes one deterministic scene query.
        /// </summary>
        /// <param name="scenePath"> The queried scene asset path. </param>
        /// <param name="queryArguments"> The parsed scene-query arguments. </param>
        /// <param name="matches"> The deduplicated matches in ascending canonical-key order when the query succeeds. </param>
        /// <param name="errorMessage"> The validation or query error message when the query fails. </param>
        /// <returns> <see langword="true" /> when the scene can be queried successfully; otherwise <see langword="false" />. </returns>
        public static bool TryQueryPersisted (
            string scenePath,
            QueryArguments queryArguments,
            out List<QueryMatch> matches,
            out string errorMessage)
        {
            matches = new List<QueryMatch>();
            errorMessage = string.Empty;
            if (!SceneSourceResolver.TryAcquire(
                    scenePath,
                    SceneSourceResolver.Policy.PersistedPreview,
                    executionContext: null,
                    out var sceneLease,
                    out errorMessage))
            {
                return false;
            }

            using (sceneLease)
            {
                if (!CollectMatches(scenePath, sceneLease.Scene, queryArguments, executionContext: null, allowTemporaryState: false, out matches, out errorMessage))
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Executes one runtime scene query against request-local plan state or current loaded state.
        /// </summary>
        /// <param name="scenePath"> The queried scene asset path. </param>
        /// <param name="queryArguments"> The parsed scene-query arguments. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to observe request-local preview-scene state during plan execution without creating new tracked preview state. </param>
        /// <param name="matches"> The deduplicated matches in ascending canonical-key order when the query succeeds. </param>
        /// <param name="errorMessage"> The validation or query error message when the query fails. </param>
        /// <returns> <see langword="true" /> when the scene can be queried successfully; otherwise <see langword="false" />. </returns>
        public static bool TryQueryRuntime (
            string scenePath,
            QueryArguments queryArguments,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out List<QueryMatch> matches,
            out string errorMessage)
        {
            matches = new List<QueryMatch>();
            errorMessage = string.Empty;
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            var policy = allowTemporaryState
                ? SceneSourceResolver.Policy.TrackedTemporaryOrLoadedOrPersistedPreview
                : SceneSourceResolver.Policy.LoadedOrPersistedPreview;
            if (!SceneSourceResolver.TryAcquire(
                    scenePath,
                    policy,
                    executionContext,
                    out var sceneLease,
                    out errorMessage))
            {
                return false;
            }

            using (sceneLease)
            {
                return CollectMatches(scenePath, sceneLease.Scene, queryArguments, executionContext, allowTemporaryState, out matches, out errorMessage);
            }
        }

        private static QueryArguments CreateQueryArguments (IpcSceneQueryArgsContract contract)
        {
            return new QueryArguments(contract.PathPrefix, contract.ComponentType);
        }

        private static bool CollectMatches (
            string scenePath,
            Scene scene,
            QueryArguments queryArguments,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out List<QueryMatch> matches,
            out string errorMessage)
        {
            var orderedMatches = new SortedDictionary<string, QueryMatch>(StringComparer.Ordinal);
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (!TryCollectMatchesRecursive(scenePath, roots[i].transform, queryArguments, executionContext, allowTemporaryState, orderedMatches, out errorMessage))
                {
                    matches = new List<QueryMatch>();
                    return false;
                }
            }

            matches = new List<QueryMatch>(orderedMatches.Values);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryCollectMatchesRecursive (
            string scenePath,
            Transform transform,
            QueryArguments queryArguments,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            SortedDictionary<string, QueryMatch> orderedMatches,
            out string errorMessage)
        {
            if (transform.name.Contains("/", StringComparison.Ordinal))
            {
                // NOTE:
                // hierarchyPath selectors use '/' as a structural delimiter, so a GameObject name that contains '/'
                // cannot be represented or re-resolved later. Skip the entire subtree instead of failing unrelated queries.
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
                if (queryArguments.ComponentType == null)
                {
                    if (!RegisterMatch(
                            orderedMatches,
                            new QueryMatch(
                                IpcEditTargetKind.GameObject,
                                hierarchyPath,
                                null,
                                CreateCanonicalKey(scenePath, IpcEditTargetKind.GameObject, hierarchyPath, null)),
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
                            queryArguments.ComponentType,
                            executionContext,
                            allowTemporaryState,
                            out var componentMatch,
                            out errorMessage))
                    {
                        return false;
                    }

                    if (componentMatch != null)
                    {
                        if (!RegisterMatch(orderedMatches, componentMatch.Value, out errorMessage))
                        {
                            return false;
                        }
                    }
                }
            }

            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                if (!TryCollectMatchesRecursive(scenePath, transform.GetChild(childIndex), queryArguments, executionContext, allowTemporaryState, orderedMatches, out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryCreateComponentMatch (
            GameObject gameObject,
            string hierarchyPath,
            string scenePath,
            string componentType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out QueryMatch? match,
            out string errorMessage)
        {
            match = null;
            if (!ComponentOperationUtilities.TryResolveComponentSelector(
                    gameObject,
                    componentType,
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
                errorMessage = $"Scene query target '{hierarchyPath}' resolved multiple components of type '{componentType}'.";
                return false;
            }

            match = new QueryMatch(
                IpcEditTargetKind.Component,
                hierarchyPath,
                componentType,
                CreateCanonicalKey(scenePath, IpcEditTargetKind.Component, hierarchyPath, componentType));
            errorMessage = string.Empty;
            return true;
        }

        private static bool RegisterMatch (
            IDictionary<string, QueryMatch> orderedMatches,
            QueryMatch match,
            out string errorMessage)
        {
            if (orderedMatches.ContainsKey(match.CanonicalKey))
            {
                errorMessage = $"Scene query resolved duplicate canonical target at '{match.HierarchyPath}'.";
                return false;
            }

            orderedMatches.Add(match.CanonicalKey, match);
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
            IpcEditTargetKind targetKind,
            string hierarchyPath,
            string? componentType)
        {
            return "scene"
                   + "\u001f"
                   + scenePath
                   + "\u001f"
                   + targetKind.ToString()
                   + "\u001f"
                   + hierarchyPath
                   + "\u001f"
                   + (componentType ?? string.Empty);
        }

        /// <summary>
        /// Represents the parsed arguments for one scene query.
        /// </summary>
        /// <param name="PathPrefix"> The optional hierarchy-path prefix filter. <see langword="null" /> matches the entire scene. </param>
        /// <param name="ComponentType"> The optional component type filter. <see langword="null" /> selects GameObjects instead of components. </param>
        internal readonly struct QueryArguments
        {
            public QueryArguments (
                string? pathPrefix,
                string? componentType)
            {
                PathPrefix = pathPrefix;
                ComponentType = componentType;
            }

            public string? PathPrefix { get; }

            public string? ComponentType { get; }
        }

        /// <summary>
        /// Represents one deterministic scene-query match.
        /// </summary>
        /// <param name="TargetKind"> The compiled target category produced by the match. </param>
        /// <param name="HierarchyPath"> The matched hierarchy path relative to the scene root. </param>
        /// <param name="ComponentType"> The matched component type. <see langword="null" /> for GameObject matches. </param>
        /// <param name="CanonicalKey"> The canonical identity key used for deduplication and ordering. </param>
        internal readonly struct QueryMatch
        {
            public QueryMatch (
                IpcEditTargetKind targetKind,
                string hierarchyPath,
                string? componentType,
                string canonicalKey)
            {
                TargetKind = targetKind;
                HierarchyPath = hierarchyPath;
                ComponentType = componentType;
                CanonicalKey = canonicalKey;
            }

            public IpcEditTargetKind TargetKind { get; }

            public string HierarchyPath { get; }

            public string? ComponentType { get; }

            public string CanonicalKey { get; }
        }
    }
}
