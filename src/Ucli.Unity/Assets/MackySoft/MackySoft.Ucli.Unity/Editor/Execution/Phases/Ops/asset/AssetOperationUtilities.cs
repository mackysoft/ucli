using System;
using System.IO;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers shared by asset-domain operations. </summary>
    internal static class AssetOperationUtilities
    {
        private const string AssetExtension = ".asset";

        /// <summary> Validates one create-path contract and returns the normalized asset path. </summary>
        /// <param name="assetPath"> The raw asset path. </param>
        /// <param name="normalizedAssetPath"> The normalized asset path when valid. </param>
        /// <param name="errorMessage"> The validation error message when validation fails. </param>
        /// <returns> <see langword="true" /> when the path is valid for <c>ucli.asset.create</c>; otherwise <see langword="false" />. </returns>
        public static bool TryValidateCreateAssetPath (
            string assetPath,
            out string normalizedAssetPath,
            out string errorMessage)
        {
            normalizedAssetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                errorMessage = "Asset path must not be empty or whitespace.";
                return false;
            }

            if (StringValueValidator.HasOuterWhitespace(assetPath))
            {
                errorMessage = "Asset path must not contain leading or trailing whitespace.";
                return false;
            }

            normalizedAssetPath = UnityAssetPathUtility.NormalizeAssetPath(assetPath);
            if (!UnityAssetPathUtility.IsAssetsDescendantPath(normalizedAssetPath))
            {
                errorMessage = $"Asset path must be under '{UnityAssetPathUtility.AssetsRootPrefix}'. Actual: {normalizedAssetPath}.";
                return false;
            }

            if (!normalizedAssetPath.EndsWith(AssetExtension, StringComparison.Ordinal))
            {
                errorMessage = $"Asset path must end with '{AssetExtension}'. Actual: {normalizedAssetPath}.";
                return false;
            }

            if (UnityAssetPathUtility.IsReservedAssetPath(normalizedAssetPath))
            {
                errorMessage = $"Asset path is reserved for another domain: {normalizedAssetPath}.";
                return false;
            }

            var directoryPath = UnityAssetPathUtility.ResolveDirectoryPath(normalizedAssetPath);
            if (!AssetDatabase.IsValidFolder(directoryPath))
            {
                errorMessage = $"Asset directory does not exist: {directoryPath}.";
                return false;
            }

            if (AssetDatabase.LoadMainAssetAtPath(normalizedAssetPath) != null || File.Exists(UnityAssetPathUtility.ToAbsolutePath(normalizedAssetPath)))
            {
                errorMessage = $"Asset path already exists: {normalizedAssetPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Resolves one reference to an existing main asset. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> Whether temporary aliases may satisfy the reference. </param>
        /// <param name="unityObject"> The resolved asset object when successful. </param>
        /// <param name="assetPath"> The resolved asset path when successful. </param>
        /// <param name="sourceGlobalObjectId"> The source global-object identifier when available. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to a supported asset target; otherwise <see langword="false" />. </returns>
        public static bool TryResolveAssetTarget (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string assetPath,
            out string? sourceGlobalObjectId,
            out string errorMessage)
        {
            unityObject = null;
            assetPath = string.Empty;
            sourceGlobalObjectId = null;
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (allowTemporaryState
                && reference.Kind == UnityObjectReferenceKind.Alias
                && executionContext.TryGetTemporaryAliasState(reference.Alias!, out var temporaryAliasState))
            {
                assetPath = temporaryAliasState.Resource.Path;
                sourceGlobalObjectId = temporaryAliasState.SourceGlobalObjectId;
                if (temporaryAliasState.Resource.Kind != OperationTouchKind.Asset)
                {
                    errorMessage = "Reference did not resolve to an asset.";
                    assetPath = string.Empty;
                    sourceGlobalObjectId = null;
                    return false;
                }

                if (!TryValidateTemporaryAssetTarget(temporaryAliasState.UnityObject!, reference.Alias!, out errorMessage))
                {
                    unityObject = null;
                    assetPath = string.Empty;
                    sourceGlobalObjectId = null;
                    return false;
                }

                unityObject = temporaryAliasState.UnityObject;
                return true;
            }

            if (allowTemporaryState
                && reference.Kind == UnityObjectReferenceKind.Selector
                && reference.Selector.Kind == ResolveSelectorKind.AssetPath
                && executionContext.TryGetPlannedAssetState(reference.Selector.AssetPath!, out var plannedAssetState))
            {
                unityObject = plannedAssetState.UnityObject;
                assetPath = plannedAssetState.AssetPath;
                sourceGlobalObjectId = null;
                errorMessage = string.Empty;
                return true;
            }

            if (!OperationObjectReferenceUtilities.TryResolveUnityObject(
                reference,
                executionContext,
                allowTemporaryState: false,
                out unityObject,
                out errorMessage))
            {
                assetPath = string.Empty;
                sourceGlobalObjectId = null;
                return false;
            }

            if (!TryValidatePersistentMainAssetTarget(unityObject!, out assetPath, out errorMessage))
            {
                unityObject = null;
                sourceGlobalObjectId = null;
                return false;
            }

            sourceGlobalObjectId = UnityObjectReferenceResolver.CreateResolvedReference(unityObject!).GlobalObjectId;
            return true;
        }

        /// <summary> Creates one temporary scriptable-object instance for plan-time asset creation. </summary>
        /// <param name="assetType"> The concrete scriptable-object type. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="unityObject"> The created temporary object when successful. </param>
        /// <param name="errorMessage"> The validation error message when creation fails. </param>
        /// <returns> <see langword="true" /> when temporary instance is created; otherwise <see langword="false" />. </returns>
        public static bool TryCreateTemporaryAssetInstance (
            Type assetType,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (assetType == null)
            {
                throw new ArgumentNullException(nameof(assetType));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            try
            {
                var scriptableObject = ScriptableObject.CreateInstance(assetType);
                if (scriptableObject == null)
                {
                    unityObject = null;
                    errorMessage = $"Temporary asset instance could not be created for type '{assetType.FullName}'.";
                    return false;
                }

                scriptableObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                executionContext.TrackTemporaryObject(scriptableObject);
                unityObject = scriptableObject;
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                unityObject = null;
                errorMessage = $"Temporary asset instance could not be created for type '{assetType.FullName}'. {exception.Message}";
                return false;
            }
        }

        /// <summary> Creates one temporary clone of a persistent asset for plan-time mutation. </summary>
        /// <param name="source"> The source asset. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="clone"> The created temporary clone when successful. </param>
        /// <param name="errorMessage"> The validation error message when cloning fails. </param>
        /// <returns> <see langword="true" /> when a temporary clone is created; otherwise <see langword="false" />. </returns>
        public static bool TryCreateTemporaryAssetClone (
            UnityEngine.Object source,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? clone,
            out string errorMessage)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            try
            {
                clone = UnityEngine.Object.Instantiate(source);
                if (clone == null)
                {
                    errorMessage = $"Temporary asset clone could not be created for type '{source.GetType().FullName}'.";
                    return false;
                }

                clone.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                executionContext.TrackTemporaryObject(clone);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                clone = null;
                errorMessage = $"Temporary asset clone could not be created for type '{source.GetType().FullName}'. {exception.Message}";
                return false;
            }
        }

        /// <summary> Copies serialized state from one asset object to another object of the same runtime type. </summary>
        /// <param name="source"> The source object. </param>
        /// <param name="target"> The target object. </param>
        public static bool TryCopySerializedState (
            UnityEngine.Object source,
            UnityEngine.Object target,
            out string errorMessage)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            try
            {
                EditorUtility.CopySerialized(source, target);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Serialized asset state could not be copied to '{target.GetType().FullName}'. {exception.Message}";
                return false;
            }
        }

        /// <summary> Creates one touched entry for the specified asset path. </summary>
        /// <param name="assetPath"> The asset path. </param>
        /// <returns> The touched asset entry. </returns>
        public static OperationTouch CreateAssetTouch (string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return new OperationTouch(
                Kind: OperationTouchKind.Asset,
                Path: assetPath,
                Guid: string.IsNullOrWhiteSpace(guid) ? null : guid);
        }

        private static bool TryValidateTemporaryAssetTarget (
            UnityEngine.Object unityObject,
            string alias,
            out string errorMessage)
        {
            if (unityObject == null)
            {
                errorMessage = $"Reference alias was not found: {alias}.";
                return false;
            }

            if (unityObject is GameObject || unityObject is Component)
            {
                errorMessage = "Reference did not resolve to an asset.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryValidatePersistentMainAssetTarget (
            UnityEngine.Object unityObject,
            out string assetPath,
            out string errorMessage)
        {
            assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                errorMessage = "Reference did not resolve to a persistent asset.";
                return false;
            }

            assetPath = UnityAssetPathUtility.NormalizeAssetPath(assetPath);
            if (!UnityAssetPathUtility.IsAssetsDescendantPath(assetPath))
            {
                errorMessage = $"Asset path must be under '{UnityAssetPathUtility.AssetsRootPrefix}'. Actual: {assetPath}.";
                return false;
            }

            if (UnityAssetPathUtility.IsReservedAssetPath(assetPath))
            {
                errorMessage = $"Asset path is reserved for another domain: {assetPath}.";
                return false;
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset == null || mainAsset != unityObject)
            {
                errorMessage = $"Reference did not resolve to a main asset: {assetPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
