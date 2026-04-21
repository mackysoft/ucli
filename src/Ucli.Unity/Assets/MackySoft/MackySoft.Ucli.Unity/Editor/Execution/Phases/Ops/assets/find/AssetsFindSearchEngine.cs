using System;
using System.Collections.Generic;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Project;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes deterministic asset search for <c>ucli.assets.find</c>. </summary>
    internal static class AssetsFindSearchEngine
    {
        public static IReadOnlyList<SearchMatch> SearchLive (SearchCriteria criteria)
        {
            return Search(criteria, executionContext: null, includeTemporaryState: false);
        }

        public static IReadOnlyList<SearchMatch> SearchWithTemporaryState (
            SearchCriteria criteria,
            OperationExecutionContext executionContext)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            return Search(criteria, executionContext, includeTemporaryState: true);
        }

        private static IReadOnlyList<SearchMatch> Search (
            SearchCriteria criteria,
            OperationExecutionContext? executionContext,
            bool includeTemporaryState)
        {
            var matchesByAssetPath = new SortedDictionary<string, SearchMatch>(StringComparer.Ordinal);
            CollectPersistedMatches(criteria, matchesByAssetPath);
            if (includeTemporaryState)
            {
                OverlayPlannedAssets(criteria, executionContext!, matchesByAssetPath);
                OverlayAssetShadows(criteria, executionContext!, matchesByAssetPath);
            }

            var matches = new SearchMatch[matchesByAssetPath.Count];
            var index = 0;
            foreach (var match in matchesByAssetPath.Values)
            {
                matches[index] = match;
                index++;
            }

            return matches;
        }

        private static void CollectPersistedMatches (
            SearchCriteria criteria,
            IDictionary<string, SearchMatch> matchesByAssetPath)
        {
            var assetPaths = AssetDatabase.GetAllAssetPaths();
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var normalizedAssetPath = UnityAssetPathUtility.NormalizeAssetPath(assetPaths[i]);
                if (!IsSearchableAssetPath(normalizedAssetPath)
                    || !MatchesPathPrefix(criteria, normalizedAssetPath))
                {
                    continue;
                }

                var mainAsset = AssetDatabase.LoadMainAssetAtPath(normalizedAssetPath);
                if (mainAsset == null)
                {
                    continue;
                }

                TrySetMatch(criteria, normalizedAssetPath, mainAsset, matchesByAssetPath);
            }
        }

        private static void OverlayPlannedAssets (
            SearchCriteria criteria,
            OperationExecutionContext executionContext,
            IDictionary<string, SearchMatch> matchesByAssetPath)
        {
            var plannedAssetStates = new List<PlannedAssetRegistry.PlannedAssetState>();
            executionContext.CollectPlannedAssetStates(plannedAssetStates);
            for (var i = 0; i < plannedAssetStates.Count; i++)
            {
                var plannedAsset = plannedAssetStates[i].UnityObject;
                if (plannedAsset == null
                    || !IsSearchableAssetPath(plannedAssetStates[i].AssetPath)
                    || !MatchesPathPrefix(criteria, plannedAssetStates[i].AssetPath))
                {
                    continue;
                }

                TrySetMatch(criteria, plannedAssetStates[i].AssetPath, plannedAsset, matchesByAssetPath);
            }
        }

        private static void OverlayAssetShadows (
            SearchCriteria criteria,
            OperationExecutionContext executionContext,
            IDictionary<string, SearchMatch> matchesByAssetPath)
        {
            var assetShadowStates = new List<AssetSandboxRegistry.AssetShadowState>();
            executionContext.CollectAssetShadowStates(assetShadowStates);
            for (var i = 0; i < assetShadowStates.Count; i++)
            {
                var shadowAssetPath = assetShadowStates[i].AssetPath;
                if (!IsSearchableAssetPath(shadowAssetPath)
                    || !MatchesPathPrefix(criteria, shadowAssetPath))
                {
                    continue;
                }

                var shadowAsset = assetShadowStates[i].UnityObject;
                if (shadowAsset == null)
                {
                    matchesByAssetPath.Remove(shadowAssetPath);
                    continue;
                }

                if (!TrySetMatch(criteria, shadowAssetPath, shadowAsset, matchesByAssetPath))
                {
                    matchesByAssetPath.Remove(shadowAssetPath);
                }
            }
        }

        private static bool TrySetMatch (
            SearchCriteria criteria,
            string assetPath,
            UnityEngine.Object unityObject,
            IDictionary<string, SearchMatch> matchesByAssetPath)
        {
            if (!MatchesType(criteria, unityObject)
                || !MatchesName(criteria, unityObject.name))
            {
                return false;
            }

            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(assetGuid))
            {
                assetGuid = string.Empty;
            }

            matchesByAssetPath[assetPath] = new SearchMatch(
                assetPath,
                assetGuid,
                unityObject.name,
                IndexTypeIdFormatter.Format(unityObject.GetType()));
            return true;
        }

        private static bool MatchesPathPrefix (
            SearchCriteria criteria,
            string assetPath)
        {
            return criteria.PathPrefix == null
                   || assetPath.StartsWith(criteria.PathPrefix, StringComparison.Ordinal);
        }

        private static bool MatchesType (
            SearchCriteria criteria,
            UnityEngine.Object unityObject)
        {
            return criteria.TypeFilter == null
                   || criteria.TypeFilter.IsAssignableFrom(unityObject.GetType());
        }

        private static bool MatchesName (
            SearchCriteria criteria,
            string assetName)
        {
            return criteria.NameContains == null
                   || assetName.IndexOf(criteria.NameContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSearchableAssetPath (string assetPath)
        {
            return UnityAssetPathUtility.IsAssetsDescendantPath(assetPath)
                   && !AssetDatabase.IsValidFolder(assetPath);
        }

        internal readonly struct SearchCriteria
        {
            public SearchCriteria (
                Type? typeFilter,
                string? pathPrefix,
                string? nameContains)
            {
                TypeFilter = typeFilter;
                PathPrefix = pathPrefix;
                NameContains = nameContains;
            }

            public Type? TypeFilter { get; }

            public string? PathPrefix { get; }

            public string? NameContains { get; }
        }

        internal readonly struct SearchMatch
        {
            public SearchMatch (
                string assetPath,
                string assetGuid,
                string name,
                string typeId)
            {
                AssetPath = assetPath;
                AssetGuid = assetGuid;
                Name = name;
                TypeId = typeId;
            }

            public string AssetPath { get; }

            public string AssetGuid { get; }

            public string Name { get; }

            public string TypeId { get; }
        }
    }
}
