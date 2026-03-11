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
                new OperationResource(OperationTouchKind.Asset, assetPath));
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
    }
}