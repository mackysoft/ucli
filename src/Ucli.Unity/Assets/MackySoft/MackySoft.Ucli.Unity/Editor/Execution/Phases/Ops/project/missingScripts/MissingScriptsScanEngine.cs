using System;
using System.Collections.Generic;
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

        public MissingScriptsCheckResult Scan (MissingScriptsCheckArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var unscannedScopes = new List<MissingScriptsUnscannedScope>();
            var discoveredAssets = DiscoverAssets(args, unscannedScopes);
            var scannedAssets = new List<UnityAssetPath>(discoveredAssets.Count);
            var unscannedAssets = new List<MissingScriptsUnscannedAsset>();
            var missingScriptSlots = new List<MissingScriptSlot>();

            foreach (var discoveredAsset in discoveredAssets.Values)
            {
                var scanOutcome = ScanAsset(discoveredAsset, missingScriptSlots);
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

            missingScriptSlots.Sort(MissingScriptSlotComparer.Instance);
            return new MissingScriptsCheckResult(
                new MissingScriptsRequestedScope(args.Roots, args.AssetKinds),
                unscannedScopes,
                scannedAssets,
                unscannedAssets,
                missingScriptSlots);
        }

        private SortedDictionary<string, DiscoveredAsset> DiscoverAssets (
            MissingScriptsCheckArgs args,
            ICollection<MissingScriptsUnscannedScope> unscannedScopes)
        {
            var discoveredAssets = new SortedDictionary<string, DiscoveredAsset>(StringComparer.Ordinal);
            for (var rootIndex = 0; rootIndex < args.Roots.Count; rootIndex++)
            {
                var root = args.Roots[rootIndex];
                for (var assetKindIndex = 0; assetKindIndex < args.AssetKinds.Count; assetKindIndex++)
                {
                    var assetKind = args.AssetKinds[assetKindIndex];
                    string[] assetGuids;
                    try
                    {
                        assetGuids = assetAccess.FindAssets(CreateAssetFilter(assetKind), new[] { root.Value });
                    }
                    catch (Exception)
                    {
                        unscannedScopes.Add(new MissingScriptsUnscannedScope(
                            root,
                            assetKind,
                            MissingScriptsUnscannedReason.ScopeReadFailed));
                        continue;
                    }

                    for (var assetGuidIndex = 0; assetGuidIndex < assetGuids.Length; assetGuidIndex++)
                    {
                        string assetPath;
                        try
                        {
                            assetPath = assetAccess.GuidToAssetPath(assetGuids[assetGuidIndex]);
                        }
                        catch (Exception)
                        {
                            AddUnscannedScope(unscannedScopes, root, assetKind);
                            break;
                        }

                        if (!UnityAssetPath.TryParse(assetPath, out var typedAssetPath)
                            || !MatchesAssetKind(typedAssetPath.Value, assetKind))
                        {
                            AddUnscannedScope(unscannedScopes, root, assetKind);
                            break;
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
            MissingScriptsAssetKind assetKind)
        {
            unscannedScopes.Add(new MissingScriptsUnscannedScope(
                root,
                assetKind,
                MissingScriptsUnscannedReason.ScopeReadFailed));
        }

        private AssetScanOutcome ScanAsset (
            DiscoveredAsset asset,
            ICollection<MissingScriptSlot> missingScriptSlots)
        {
            return asset.AssetKind == MissingScriptsAssetKind.Scene
                ? ScanScene(asset.AssetPath, missingScriptSlots)
                : ScanPrefab(asset.AssetPath, missingScriptSlots);
        }

        private AssetScanOutcome ScanScene (
            string assetPath,
            ICollection<MissingScriptSlot> missingScriptSlots)
        {
            if (!assetAccess.IsSceneAsset(assetPath))
            {
                return AssetScanOutcome.AssetChanged;
            }

            if (!assetAccess.TryAcquirePersistedPreview(assetPath, out var sceneLease))
            {
                return AssetScanOutcome.AssetReadFailed;
            }

            using (sceneLease)
            {
                return CollectMissingScriptSlots(assetPath, sceneLease.Scene, missingScriptSlots)
                    ? AssetScanOutcome.Scanned
                    : AssetScanOutcome.HierarchyPathUnrepresentable;
            }
        }

        private AssetScanOutcome ScanPrefab (
            string assetPath,
            ICollection<MissingScriptSlot> missingScriptSlots)
        {
            if (!assetAccess.IsPrefabAsset(assetPath))
            {
                return AssetScanOutcome.AssetChanged;
            }

            GameObject prefabRoot;
            try
            {
                prefabRoot = assetAccess.LoadPrefabContents(assetPath);
            }
            catch (Exception)
            {
                return AssetScanOutcome.AssetReadFailed;
            }

            try
            {
                return CollectMissingScriptSlots(assetPath, prefabRoot, missingScriptSlots)
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
            ICollection<MissingScriptSlot> missingScriptSlots)
        {
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (!CollectMissingScriptSlots(assetPath, roots[rootIndex].transform, missingScriptSlots))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CollectMissingScriptSlots (
            string assetPath,
            GameObject prefabRoot,
            ICollection<MissingScriptSlot> missingScriptSlots)
        {
            return CollectMissingScriptSlots(assetPath, prefabRoot.transform, missingScriptSlots);
        }

        private static bool CollectMissingScriptSlots (
            string assetPath,
            Transform transform,
            ICollection<MissingScriptSlot> missingScriptSlots)
        {
            if (!UnityHierarchyPath.TryParse(CreateHierarchyPath(transform), out var hierarchyPath))
            {
                return false;
            }

            var components = transform.gameObject.GetComponents<Component>();
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                if (components[componentIndex] == null)
                {
                    missingScriptSlots.Add(new MissingScriptSlot(
                        new UnityAssetPath(assetPath),
                        hierarchyPath,
                        componentIndex));
                }
            }

            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                if (!CollectMissingScriptSlots(assetPath, transform.GetChild(childIndex), missingScriptSlots))
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
    }
}
