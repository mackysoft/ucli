using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks asset-specific plan-time shadows independently from other execution state. </summary>
    internal sealed class AssetSandboxRegistry
    {
        private readonly Dictionary<UnityGlobalObjectId, AssetShadowValue> assetShadowsByGlobalObjectId =
            new Dictionary<UnityGlobalObjectId, AssetShadowValue>();

        /// <summary> Stores or replaces one plan-time asset shadow and advances aliases bound to the same stable source. </summary>
        /// <param name="sourceGlobalObjectId"> The stable identity of the persisted source asset. </param>
        /// <param name="unityObject"> The live request-local shadow. </param>
        /// <param name="assetPath"> The persisted source asset path. </param>
        /// <param name="temporaryAliasRegistry"> The alias registry to synchronize. </param>
        /// <exception cref="ArgumentException"> <paramref name="assetPath" /> is missing. </exception>
        /// <exception cref="ArgumentNullException"> An object or registry argument is <see langword="null" />. </exception>
        public void SetAssetShadow (
            UnityGlobalObjectId sourceGlobalObjectId,
            UnityEngine.Object unityObject,
            string assetPath,
            TemporaryAliasRegistry temporaryAliasRegistry)
        {
            if (sourceGlobalObjectId == null)
            {
                throw new ArgumentNullException(nameof(sourceGlobalObjectId));
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

            assetShadowsByGlobalObjectId[sourceGlobalObjectId] = new AssetShadowValue(unityObject, assetPath);
            temporaryAliasRegistry.SynchronizeBySourceTrackingKey(
                RequestLocalObjectIdentity.FromGlobalObjectId(sourceGlobalObjectId),
                unityObject,
                OperationResource.PersistentAsset(assetPath));
        }

        /// <summary> Tries to get the live request-local shadow for one stable source asset. </summary>
        /// <param name="sourceGlobalObjectId"> The stable identity of the persisted source asset. </param>
        /// <param name="unityObject"> The live shadow when found; otherwise <see langword="null" />. </param>
        /// <param name="assetPath"> The associated persisted asset path when found; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when a non-destroyed shadow exists; otherwise <see langword="false" />. </returns>
        public bool TryGetAssetShadow (
            UnityGlobalObjectId sourceGlobalObjectId,
            [NotNullWhen(true)] out UnityEngine.Object? unityObject,
            out string assetPath)
        {
            unityObject = null;
            assetPath = string.Empty;
            if (sourceGlobalObjectId == null)
            {
                return false;
            }

            if (!assetShadowsByGlobalObjectId.TryGetValue(sourceGlobalObjectId, out var value))
            {
                return false;
            }

            if (value.UnityObject == null)
            {
                assetShadowsByGlobalObjectId.Remove(sourceGlobalObjectId);
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
                UnityGlobalObjectId sourceGlobalObjectId,
                UnityEngine.Object unityObject,
                string assetPath)
            {
                SourceGlobalObjectId = sourceGlobalObjectId;
                UnityObject = unityObject;
                AssetPath = assetPath;
            }

            /// <summary> Gets the stable identity of the persisted source object that the shadow replaces. </summary>
            public UnityGlobalObjectId SourceGlobalObjectId { get; }

            /// <summary> Gets the live shadow object. Collected states always provide a non-destroyed object. </summary>
            public UnityEngine.Object UnityObject { get; }

            /// <summary> Gets the asset path that remains associated with the shadowed persistent asset. </summary>
            public string AssetPath { get; }
        }
    }
}
