using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks asset-specific plan-time shadows independently from other execution state. </summary>
    internal sealed class AssetSandboxRegistry
    {
        private readonly Dictionary<string, AssetShadowValue> assetShadowsByGlobalObjectId =
            new Dictionary<string, AssetShadowValue>(StringComparer.Ordinal);

        public void SetAssetShadow (
            string globalObjectId,
            UnityEngine.Object unityObject,
            string assetPath,
            TemporaryAliasRegistry temporaryAliasRegistry)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must not be null, empty, or whitespace.", nameof(globalObjectId));
            }

            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path must not be null, empty, or whitespace.", nameof(assetPath));
            }

            if (temporaryAliasRegistry == null)
            {
                throw new ArgumentNullException(nameof(temporaryAliasRegistry));
            }

            assetShadowsByGlobalObjectId[globalObjectId] = new AssetShadowValue(unityObject, assetPath);
            temporaryAliasRegistry.SynchronizeBySourceGlobalObjectId(
                globalObjectId,
                unityObject,
                OperationResource.PersistentAsset(assetPath));
        }

        public bool TryGetAssetShadow (
            string globalObjectId,
            out UnityEngine.Object? unityObject,
            out string assetPath)
        {
            unityObject = null;
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                return false;
            }

            if (!assetShadowsByGlobalObjectId.TryGetValue(globalObjectId, out var value))
            {
                return false;
            }

            if (value.UnityObject == null)
            {
                assetShadowsByGlobalObjectId.Remove(globalObjectId);
                return false;
            }

            unityObject = value.UnityObject;
            assetPath = value.AssetPath;
            return true;
        }

        /// <summary> Collects live asset shadow states into the specified destination. Destroyed shadow objects are omitted. </summary>
        /// <param name="destination"> The collection that receives the current live asset shadow states. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="destination"/> is <see langword="null"/>. </exception>
        public void CollectAssetShadowStates (ICollection<AssetShadowState> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            foreach (var pair in assetShadowsByGlobalObjectId)
            {
                if (pair.Value.UnityObject == null)
                {
                    continue;
                }

                destination.Add(new AssetShadowState(
                    pair.Key,
                    pair.Value.UnityObject,
                    pair.Value.AssetPath));
            }
        }

        public void Clear ()
        {
            assetShadowsByGlobalObjectId.Clear();
        }

        private readonly struct AssetShadowValue
        {
            public AssetShadowValue (
                UnityEngine.Object unityObject,
                string assetPath)
            {
                UnityObject = unityObject;
                AssetPath = assetPath;
            }

            public UnityEngine.Object UnityObject { get; }

            public string AssetPath { get; }
        }

        /// <summary> Represents a live asset shadow that overrides a persisted asset during plan-time queries. </summary>
        internal readonly struct AssetShadowState
        {
            public AssetShadowState (
                string sourceGlobalObjectId,
                UnityEngine.Object unityObject,
                string assetPath)
            {
                SourceGlobalObjectId = sourceGlobalObjectId;
                UnityObject = unityObject;
                AssetPath = assetPath;
            }

            /// <summary> Gets the source persistent object's global identifier that the shadow replaces. </summary>
            public string SourceGlobalObjectId { get; }

            /// <summary> Gets the live shadow object. Collected states always provide a non-destroyed object. </summary>
            public UnityEngine.Object? UnityObject { get; }

            /// <summary> Gets the asset path that remains associated with the shadowed persistent asset. </summary>
            public string AssetPath { get; }
        }
    }
}
