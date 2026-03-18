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
            string ownerOperationId,
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

            if (string.IsNullOrWhiteSpace(ownerOperationId))
            {
                throw new ArgumentException("Owner operation id must not be null, empty, or whitespace.", nameof(ownerOperationId));
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
                    new OperationResource(OperationTouchKind.Asset, normalizedAssetPath));
            }

            valuesByAssetPath[normalizedAssetPath] = new PlannedAssetValue(ownerOperationId, unityObject);
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

            state = new PlannedAssetState(value.OwnerOperationId, normalizedAssetPath, value.UnityObject);
            return true;
        }

        public void Clear ()
        {
            valuesByAssetPath.Clear();
        }

        private readonly struct PlannedAssetValue
        {
            public PlannedAssetValue (
                string ownerOperationId,
                UnityEngine.Object unityObject)
            {
                OwnerOperationId = ownerOperationId;
                UnityObject = unityObject;
            }

            public string OwnerOperationId { get; }

            public UnityEngine.Object UnityObject { get; }
        }

        internal readonly struct PlannedAssetState
        {
            public PlannedAssetState (
                string ownerOperationId,
                string assetPath,
                UnityEngine.Object unityObject)
            {
                OwnerOperationId = ownerOperationId;
                AssetPath = assetPath;
                UnityObject = unityObject;
            }

            public string OwnerOperationId { get; }

            public string AssetPath { get; }

            public UnityEngine.Object? UnityObject { get; }
        }
    }
}