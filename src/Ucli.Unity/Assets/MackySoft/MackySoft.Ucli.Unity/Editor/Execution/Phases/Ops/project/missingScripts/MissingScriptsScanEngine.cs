using System;
using System.Collections.Generic;
using System.Threading;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Scans saved scene and prefab assets without retaining editor-visible open state. </summary>
    internal sealed class MissingScriptsScanEngine : IMissingScriptsScanEngine
    {
        private readonly IMissingScriptsAssetAccess assetAccess;

        internal MissingScriptsScanEngine (IMissingScriptsAssetAccess assetAccess)
        {
            this.assetAccess = assetAccess ?? throw new ArgumentNullException(nameof(assetAccess));
        }

        public MissingScriptsCheckResult Scan (
            MissingScriptsCheckArgs args,
            CancellationToken cancellationToken)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var unscannedScopes = new List<MissingScriptsUnscannedScope>();
            var unscannedAssets = new List<MissingScriptsUnscannedAsset>();
            var discoveredAssets = DiscoverAssets(args, unscannedScopes, unscannedAssets, cancellationToken);
            var scannedAssets = new List<UnityAssetPath>(discoveredAssets.Count);
            var missingScriptSlots = new List<MissingScriptSlot>();

            foreach (var discoveredAsset in discoveredAssets.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scanOutcome = ScanAsset(discoveredAsset, missingScriptSlots, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                switch (scanOutcome)
                {
                    case AssetScanOutcome.Scanned:
                        scannedAssets.Add(new UnityAssetPath(discoveredAsset.AssetPath));
                        break;

                    case AssetScanOutcome.AssetChanged:
                        unscannedAssets.Add(new MissingScriptsUnscannedAsset(
                            new UnityAssetPath(discoveredAsset.AssetPath),
                            discoveredAsset.AssetKind,
                            MissingScriptsUnscannedReason.AssetChanged));
                        break;

                    case AssetScanOutcome.AssetReadFailed:
                        unscannedAssets.Add(new MissingScriptsUnscannedAsset(
                            new UnityAssetPath(discoveredAsset.AssetPath),
                            discoveredAsset.AssetKind,
                            MissingScriptsUnscannedReason.AssetReadFailed));
                        break;

                    case AssetScanOutcome.HierarchyPathUnrepresentable:
                        unscannedAssets.Add(new MissingScriptsUnscannedAsset(
                            new UnityAssetPath(discoveredAsset.AssetPath),
                            discoveredAsset.AssetKind,
                            MissingScriptsUnscannedReason.HierarchyPathUnrepresentable));
                        break;

                    default:
                        throw new InvalidOperationException($"Unexpected missing script asset scan outcome: {scanOutcome}.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            missingScriptSlots.Sort(MissingScriptSlotComparer.Instance);
            cancellationToken.ThrowIfCancellationRequested();
            unscannedScopes.Sort(MissingScriptsUnscannedScopeComparer.Instance);
            cancellationToken.ThrowIfCancellationRequested();
            unscannedAssets.Sort(MissingScriptsUnscannedAssetComparer.Instance);
            cancellationToken.ThrowIfCancellationRequested();
            var result = new MissingScriptsCheckResult(
                new MissingScriptsRequestedScope(args.Roots, args.AssetKinds),
                unscannedScopes,
                scannedAssets,
                unscannedAssets,
                missingScriptSlots);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        private SortedDictionary<string, DiscoveredAsset> DiscoverAssets (
            MissingScriptsCheckArgs args,
            ICollection<MissingScriptsUnscannedScope> unscannedScopes,
            ICollection<MissingScriptsUnscannedAsset> unscannedAssets,
            CancellationToken cancellationToken)
        {
            var discoveredAssets = new SortedDictionary<string, DiscoveredAsset>(StringComparer.Ordinal);
            for (var rootIndex = 0; rootIndex < args.Roots.Count; rootIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = args.Roots[rootIndex];
                for (var assetKindIndex = 0; assetKindIndex < args.AssetKinds.Count; assetKindIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var assetKind = args.AssetKinds[assetKindIndex];
                    var discovery = assetAccess.FindAssets(CreateAssetFilter(assetKind), new[] { root.Value });
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!discovery.IsAvailable)
                    {
                        AddUnscannedScope(unscannedScopes, root, assetKind, MissingScriptsUnscannedReason.ScopeReadFailed);
                        continue;
                    }

                    var assetGuids = discovery.Value;
                    for (var assetGuidIndex = 0; assetGuidIndex < assetGuids.Count; assetGuidIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var resolvedPath = assetAccess.GuidToAssetPath(assetGuids[assetGuidIndex]);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!resolvedPath.IsAvailable)
                        {
                            AddUnscannedScope(unscannedScopes, root, assetKind, MissingScriptsUnscannedReason.AssetChanged);
                            continue;
                        }

                        var assetPath = resolvedPath.Value;
                        if (!UnityAssetPath.TryParse(assetPath, out var typedAssetPath)
                            || !MatchesAssetKind(typedAssetPath.Value, assetKind))
                        {
                            if (typedAssetPath == null)
                            {
                                AddUnscannedScope(unscannedScopes, root, assetKind, MissingScriptsUnscannedReason.AssetChanged);
                            }
                            else
                            {
                                unscannedAssets.Add(new MissingScriptsUnscannedAsset(
                                    new UnityAssetPath(typedAssetPath.Value),
                                    assetKind,
                                    MissingScriptsUnscannedReason.AssetChanged));
                            }

                            continue;
                        }

                        discoveredAssets.TryAdd(typedAssetPath.Value, new DiscoveredAsset(typedAssetPath.Value, assetKind));
                    }
                }
            }

            return discoveredAssets;
        }

        private static void AddUnscannedScope (
            ICollection<MissingScriptsUnscannedScope> unscannedScopes,
            UnityAssetPathPrefix root,
            MissingScriptsAssetKind assetKind,
            MissingScriptsUnscannedReason reason)
        {
            foreach (var existingScope in unscannedScopes)
            {
                if (existingScope.Root.Value == root.Value && existingScope.AssetKind == assetKind)
                {
                    return;
                }
            }

            unscannedScopes.Add(new MissingScriptsUnscannedScope(
                root,
                assetKind,
                reason));
        }

        private AssetScanOutcome ScanAsset (
            DiscoveredAsset asset,
            ICollection<MissingScriptSlot> missingScriptSlots,
            CancellationToken cancellationToken)
        {
            return asset.AssetKind == MissingScriptsAssetKind.Scene
                ? ScanScene(asset.AssetPath, missingScriptSlots, cancellationToken)
                : ScanPrefab(asset.AssetPath, missingScriptSlots, cancellationToken);
        }

        private AssetScanOutcome ScanScene (
            string assetPath,
            ICollection<MissingScriptSlot> missingScriptSlots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sceneAsset = assetAccess.IsSceneAsset(assetPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!sceneAsset.IsAvailable)
            {
                return AssetScanOutcome.AssetChanged;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var sceneSource = assetAccess.TryAcquirePersistedPreview(assetPath);
            if (!sceneSource.IsAvailable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AssetScanOutcome.AssetReadFailed;
            }

            using (var sceneLease = sceneSource.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CollectMissingScriptSlots(assetPath, sceneLease.Scene, missingScriptSlots, cancellationToken)
                    ? AssetScanOutcome.Scanned
                    : AssetScanOutcome.HierarchyPathUnrepresentable;
            }
        }

        private AssetScanOutcome ScanPrefab (
            string assetPath,
            ICollection<MissingScriptSlot> missingScriptSlots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefabAsset = assetAccess.IsPrefabAsset(assetPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!prefabAsset.IsAvailable)
            {
                return AssetScanOutcome.AssetChanged;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var prefabContents = assetAccess.LoadPrefabContents(assetPath);
            if (!prefabContents.IsAvailable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return AssetScanOutcome.AssetReadFailed;
            }

            var prefabRoot = prefabContents.Value;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CollectMissingScriptSlots(assetPath, prefabRoot, missingScriptSlots, cancellationToken)
                    ? AssetScanOutcome.Scanned
                    : AssetScanOutcome.HierarchyPathUnrepresentable;
            }
            finally
            {
                assetAccess.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool CollectMissingScriptSlots (
            string assetPath,
            Scene scene,
            ICollection<MissingScriptSlot> missingScriptSlots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roots = scene.GetRootGameObjects();
            cancellationToken.ThrowIfCancellationRequested();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CollectMissingScriptSlots(assetPath, roots[rootIndex].transform, missingScriptSlots, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CollectMissingScriptSlots (
            string assetPath,
            GameObject prefabRoot,
            ICollection<MissingScriptSlot> missingScriptSlots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CollectMissingScriptSlots(assetPath, prefabRoot.transform, missingScriptSlots, cancellationToken);
        }

        private static bool CollectMissingScriptSlots (
            string assetPath,
            Transform transform,
            ICollection<MissingScriptSlot> missingScriptSlots,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!UnityHierarchyPath.TryParse(CreateHierarchyPath(transform, cancellationToken), out var hierarchyPath))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var components = transform.gameObject.GetComponents<Component>();
            cancellationToken.ThrowIfCancellationRequested();
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (components[componentIndex] == null)
                {
                    missingScriptSlots.Add(new MissingScriptSlot(
                        new UnityAssetPath(assetPath),
                        hierarchyPath,
                        componentIndex));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var childCount = transform.childCount;
            cancellationToken.ThrowIfCancellationRequested();
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var child = transform.GetChild(childIndex);
                cancellationToken.ThrowIfCancellationRequested();
                if (!CollectMissingScriptSlots(assetPath, child, missingScriptSlots, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }

        private static string CreateAssetFilter (MissingScriptsAssetKind assetKind)
        {
            return assetKind switch
            {
                MissingScriptsAssetKind.Scene => "t:Scene",
                MissingScriptsAssetKind.Prefab => "t:Prefab",
                _ => throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Missing script asset kind must be scene or prefab."),
            };
        }

        private static bool MatchesAssetKind (
            string assetPath,
            MissingScriptsAssetKind assetKind)
        {
            return assetKind switch
            {
                MissingScriptsAssetKind.Scene => UnityAssetPathContract.IsNormalizedSceneAssetPath(assetPath),
                MissingScriptsAssetKind.Prefab => UnityAssetPathContract.IsNormalizedPrefabAssetPath(assetPath),
                _ => throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Missing script asset kind must be scene or prefab."),
            };
        }

        private static string CreateHierarchyPath (Transform transform, CancellationToken cancellationToken)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                names.Push(current.name);
                cancellationToken.ThrowIfCancellationRequested();
                current = current.parent;
                cancellationToken.ThrowIfCancellationRequested();
            }

            return string.Join("/", names);
        }

        private readonly struct DiscoveredAsset
        {
            public DiscoveredAsset (
                string assetPath,
                MissingScriptsAssetKind assetKind)
            {
                AssetPath = assetPath;
                AssetKind = assetKind;
            }

            public string AssetPath { get; }

            public MissingScriptsAssetKind AssetKind { get; }
        }

        private enum AssetScanOutcome
        {
            Scanned,
            AssetChanged,
            AssetReadFailed,
            HierarchyPathUnrepresentable,
        }

        private sealed class MissingScriptSlotComparer : IComparer<MissingScriptSlot>
        {
            public static readonly MissingScriptSlotComparer Instance = new();

            public int Compare (MissingScriptSlot? left, MissingScriptSlot? right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                var assetPathComparison = string.CompareOrdinal(left.AssetPath.Value, right.AssetPath.Value);
                if (assetPathComparison != 0)
                {
                    return assetPathComparison;
                }

                var hierarchyPathComparison = string.CompareOrdinal(left.HierarchyPath.Value, right.HierarchyPath.Value);
                return hierarchyPathComparison != 0
                    ? hierarchyPathComparison
                    : left.ComponentIndex.CompareTo(right.ComponentIndex);
            }
        }

        private sealed class MissingScriptsUnscannedAssetComparer : IComparer<MissingScriptsUnscannedAsset>
        {
            public static readonly MissingScriptsUnscannedAssetComparer Instance = new();

            public int Compare (MissingScriptsUnscannedAsset? left, MissingScriptsUnscannedAsset? right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                var assetPathComparison = string.CompareOrdinal(left.AssetPath.Value, right.AssetPath.Value);
                if (assetPathComparison != 0)
                {
                    return assetPathComparison;
                }

                var assetKindComparison = left.AssetKind.CompareTo(right.AssetKind);
                return assetKindComparison != 0
                    ? assetKindComparison
                    : left.Reason.CompareTo(right.Reason);
            }
        }

        private sealed class MissingScriptsUnscannedScopeComparer : IComparer<MissingScriptsUnscannedScope>
        {
            public static readonly MissingScriptsUnscannedScopeComparer Instance = new();

            public int Compare (MissingScriptsUnscannedScope? left, MissingScriptsUnscannedScope? right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                var rootComparison = string.CompareOrdinal(left.Root.Value, right.Root.Value);
                if (rootComparison != 0)
                {
                    return rootComparison;
                }

                var assetKindComparison = left.AssetKind.CompareTo(right.AssetKind);
                return assetKindComparison != 0
                    ? assetKindComparison
                    : left.Reason.CompareTo(right.Reason);
            }
        }
    }
}
