using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks plan-time created assets keyed by their reserved asset path. </summary>
    internal sealed class PlannedAssetRegistry
    {
        private readonly Dictionary<string, PlannedAssetValue> valuesByAssetPath =
            new Dictionary<string, PlannedAssetValue>(StringComparer.Ordinal);

        public void SetPlannedAsset (
            string assetPath,
            string ownerExecutionKey,
            UnityEngine.Object unityObject,
            TemporaryAliasRegistry temporaryAliasRegistry)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path must not be null, empty, or whitespace.", nameof(assetPath));
            }

            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            if (string.IsNullOrWhiteSpace(ownerExecutionKey))
            {
                throw new ArgumentException("Owner execution key must not be null, empty, or whitespace.", nameof(ownerExecutionKey));
            }

            if (temporaryAliasRegistry == null)
            {
                throw new ArgumentNullException(nameof(temporaryAliasRegistry));
            }

            var normalizedAssetPath = UnityAssetPathUtility.NormalizeAssetPath(assetPath);
            if (valuesByAssetPath.TryGetValue(normalizedAssetPath, out var previousValue)
                && previousValue.UnityObject != null
                && previousValue.UnityObject != unityObject)
            {
                temporaryAliasRegistry.ReplaceTrackedObject(
                    previousValue.UnityObject,
                    unityObject,
                    OperationResource.PersistentAsset(normalizedAssetPath));
            }

            valuesByAssetPath[normalizedAssetPath] = new PlannedAssetValue(ownerExecutionKey, unityObject);
        }

        public bool TryGetState (
            string assetPath,
            out PlannedAssetState state)
        {
            state = default;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var normalizedAssetPath = UnityAssetPathUtility.NormalizeAssetPath(assetPath);
            if (!valuesByAssetPath.TryGetValue(normalizedAssetPath, out var value))
            {
                return false;
            }

            if (value.UnityObject == null)
            {
                valuesByAssetPath.Remove(normalizedAssetPath);
                return false;
            }

            state = new PlannedAssetState(value.OwnerExecutionKey, normalizedAssetPath, value.UnityObject);
            return true;
        }

        public void CollectPlannedAssetStates (ICollection<PlannedAssetState> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            foreach (var pair in valuesByAssetPath)
            {
                if (pair.Value.UnityObject == null)
                {
                    continue;
                }

                destination.Add(new PlannedAssetState(
                    pair.Value.OwnerExecutionKey,
                    pair.Key,
                    pair.Value.UnityObject));
            }
        }

        public void Clear ()
        {
            valuesByAssetPath.Clear();
        }

        private readonly struct PlannedAssetValue
        {
            public PlannedAssetValue (
                string ownerExecutionKey,
                UnityEngine.Object unityObject)
            {
                OwnerExecutionKey = ownerExecutionKey;
                UnityObject = unityObject;
            }

            public string OwnerExecutionKey { get; }

            public UnityEngine.Object UnityObject { get; }
        }

        internal readonly struct PlannedAssetState
        {
            public PlannedAssetState (
                string ownerExecutionKey,
                string assetPath,
                UnityEngine.Object unityObject)
            {
                OwnerExecutionKey = ownerExecutionKey;
                AssetPath = assetPath;
                UnityObject = unityObject;
            }

            public string OwnerExecutionKey { get; }

            public string AssetPath { get; }

            public UnityEngine.Object? UnityObject { get; }
        }
    }
}
