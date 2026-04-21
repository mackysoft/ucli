using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Project;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds one deterministic asset lookup snapshot from persistent main assets under <c>Assets/</c>. </summary>
    internal sealed class AssetLookupSnapshotBuilder : IAssetLookupSnapshotBuilder
    {
        /// <inheritdoc />
        public ValueTask<IpcIndexAssetsReadResponse> Build (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshotEntries = new List<SnapshotEntry>();
            var assetPaths = AssetDatabase.GetAllAssetPaths();
            for (var i = 0; i < assetPaths.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalizedAssetPath = UnityAssetPathUtility.NormalizeAssetPath(assetPaths[i]);
                if (!UnityAssetPathUtility.IsAssetsDescendantPath(normalizedAssetPath)
                    || AssetDatabase.IsValidFolder(normalizedAssetPath))
                {
                    continue;
                }

                var mainAsset = AssetDatabase.LoadMainAssetAtPath(normalizedAssetPath);
                if (mainAsset == null)
                {
                    continue;
                }

                // NOTE:
                // AssetDatabase can temporarily report an empty GUID while the main asset still exists.
                // Keep the asset-search entry so read-index search stays aligned with live assets.find results.
                var assetGuid = AssetDatabase.AssetPathToGUID(normalizedAssetPath);
                if (string.IsNullOrEmpty(assetGuid))
                {
                    assetGuid = string.Empty;
                }

                var assetType = mainAsset.GetType();
                snapshotEntries.Add(new SnapshotEntry(
                    normalizedAssetPath,
                    assetGuid,
                    mainAsset.name,
                    IndexTypeIdFormatter.Format(assetType),
                    BuildSearchTypeIds(assetType)));
            }

            snapshotEntries.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.AssetPath, y.AssetPath));

            var assetSearchEntries = new IndexAssetSearchEntryJsonContract[snapshotEntries.Count];
            var guidPathEntries = new List<IndexGuidPathEntryJsonContract>(snapshotEntries.Count);
            for (var i = 0; i < snapshotEntries.Count; i++)
            {
                var entry = snapshotEntries[i];
                assetSearchEntries[i] = new IndexAssetSearchEntryJsonContract(
                    AssetPath: entry.AssetPath,
                    AssetGuid: entry.AssetGuid,
                    Name: entry.Name,
                    TypeId: entry.TypeId,
                    SearchTypeIds: entry.SearchTypeIds);
                if (!string.IsNullOrEmpty(entry.AssetGuid))
                {
                    guidPathEntries.Add(new IndexGuidPathEntryJsonContract(
                        AssetGuid: entry.AssetGuid,
                        AssetPath: entry.AssetPath));
                }
            }

            return new ValueTask<IpcIndexAssetsReadResponse>(new IpcIndexAssetsReadResponse(
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                AssetSearchEntries: assetSearchEntries,
                GuidPathEntries: guidPathEntries));
        }

        private static IReadOnlyList<string> BuildSearchTypeIds (Type assetType)
        {
            var searchTypeIds = new List<string>();
            for (var currentType = assetType;
                 currentType != null && typeof(UnityEngine.Object).IsAssignableFrom(currentType);
                 currentType = currentType.BaseType)
            {
                searchTypeIds.Add(IndexTypeIdFormatter.Format(currentType));
            }

            return searchTypeIds;
        }

        private readonly struct SnapshotEntry
        {
            public SnapshotEntry (
                string assetPath,
                string assetGuid,
                string name,
                string typeId,
                IReadOnlyList<string> searchTypeIds)
            {
                AssetPath = assetPath;
                AssetGuid = assetGuid;
                Name = name;
                TypeId = typeId;
                SearchTypeIds = searchTypeIds;
            }

            public string AssetPath { get; }

            public string AssetGuid { get; }

            public string Name { get; }

            public string TypeId { get; }

            public IReadOnlyList<string> SearchTypeIds { get; }
        }
    }
}
