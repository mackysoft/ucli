using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Unity.Project;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers shared by asset-domain operations. </summary>
    internal static class AssetOperationUtilities
    {
        private const string AssetExtension = ".asset";

        private const string ProjectSettingsRootPrefix = "ProjectSettings/";

        /// <summary> Validates one create-path contract and returns the normalized asset path. </summary>
        /// <param name="assetPath"> The raw asset path. </param>
        /// <param name="normalizedAssetPath"> The normalized asset path when valid. </param>
        /// <param name="errorMessage"> The validation error message when validation fails. </param>
        /// <returns> <see langword="true" /> when the path is valid for <c>ucli.asset.create</c>; otherwise <see langword="false" />. </returns>
        public static bool TryValidateCreateAssetPath (
            string? assetPath,
            out string normalizedAssetPath,
            out string errorMessage)
        {
            normalizedAssetPath = string.Empty;
            if (assetPath == null || string.IsNullOrWhiteSpace(assetPath))
            {
                errorMessage = "Asset path must not be empty or whitespace.";
                return false;
            }

            if (StringValueValidator.HasOuterWhitespace(assetPath))
            {
                errorMessage = "Asset path must not contain leading or trailing whitespace.";
                return false;
            }

            if (!UnityAssetPathContract.TryNormalizeAssetsDescendantPath(assetPath, out normalizedAssetPath))
            {
                errorMessage = $"Asset path must be under '{UnityAssetPathContract.AssetsRootPrefix}'. Actual: {assetPath}.";
                return false;
            }

            if (!normalizedAssetPath.EndsWith(AssetExtension, StringComparison.Ordinal))
            {
                errorMessage = $"Asset path must end with '{AssetExtension}'. Actual: {normalizedAssetPath}.";
                return false;
            }

            if (IsReservedAssetPath(normalizedAssetPath))
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
        /// <param name="sourceGlobalObjectId"> The stable source identity when available. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to a supported asset target; otherwise <see langword="false" />. </returns>
        public static bool TryResolveAssetTarget (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            [NotNullWhen(true)] out UnityEngine.Object? unityObject,
            [NotNullWhen(true)] out string? assetPath,
            out UnityGlobalObjectId? sourceGlobalObjectId,
            out string errorMessage)
        {
            unityObject = null;
            assetPath = string.Empty;
            sourceGlobalObjectId = null;
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            var alias = reference.Alias;
            if (allowTemporaryState
                && reference.Kind == UnityObjectReferenceKind.Alias
                && alias != null
                && executionContext.TryGetTemporaryAliasState(alias, out var temporaryAliasState))
            {
                assetPath = temporaryAliasState.Resource.Path;
                temporaryAliasState.SourceTrackingKey?.TryGetStableGlobalObjectId(out sourceGlobalObjectId);
                if (temporaryAliasState.Resource.Kind != OperationTouchKind.Asset
                    && temporaryAliasState.Resource.Kind != OperationTouchKind.ProjectSettings)
                {
                    errorMessage = "Reference did not resolve to an asset.";
                    assetPath = string.Empty;
                    sourceGlobalObjectId = null;
                    return false;
                }

                var temporaryObject = temporaryAliasState.UnityObject;
                if (temporaryObject == null)
                {
                    errorMessage = $"Reference alias was not found: {alias}.";
                    assetPath = string.Empty;
                    sourceGlobalObjectId = null;
                    return false;
                }

                if (!TryValidateTemporaryAssetTarget(temporaryObject, alias, out errorMessage))
                {
                    unityObject = null;
                    assetPath = string.Empty;
                    sourceGlobalObjectId = null;
                    return false;
                }

                unityObject = temporaryObject;
                return true;
            }

            var selectorAssetPath = reference.Selector.AssetPath;
            if (allowTemporaryState
                && reference.Kind == UnityObjectReferenceKind.Selector
                && reference.Selector.Kind == ResolveSelectorKind.AssetPath
                && selectorAssetPath != null
                && executionContext.TryGetPlannedAssetState(selectorAssetPath, out var plannedAssetState))
            {
                var plannedObject = plannedAssetState.UnityObject;
                if (plannedObject == null)
                {
                    errorMessage = $"Planned asset is no longer available: {plannedAssetState.AssetPath}.";
                    assetPath = string.Empty;
                    return false;
                }

                unityObject = plannedObject;
                assetPath = plannedAssetState.AssetPath;
                sourceGlobalObjectId = null;
                errorMessage = string.Empty;
                return true;
            }

            if (!OperationObjectReferenceUtilities.TryResolveUnityObject(
                reference,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.LiveOnly,
                out var resolvedObject,
                out errorMessage))
            {
                assetPath = string.Empty;
                sourceGlobalObjectId = null;
                return false;
            }

            var liveUnityObject = resolvedObject.UnityObject;

            if (!TryValidatePersistentMainAssetTarget(liveUnityObject, out assetPath, out errorMessage))
            {
                unityObject = null;
                sourceGlobalObjectId = null;
                return false;
            }

            unityObject = liveUnityObject;
            UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(liveUnityObject, out sourceGlobalObjectId);
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
            return OperationResourceUtilities.CreateTouch(OperationResource.PersistentAsset(assetPath));
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

            assetPath = PathStringNormalizer.ToSlashSeparated(assetPath);
            if (IsProjectSettingsAssetPath(assetPath))
            {
                return TryValidatePersistentMainAssetIdentity(unityObject, assetPath, out errorMessage);
            }

            if (!UnityAssetPathContract.IsNormalizedAssetsDescendantPath(assetPath))
            {
                errorMessage = $"Asset path must be under '{UnityAssetPathContract.AssetsRootPrefix}'. Actual: {assetPath}.";
                return false;
            }

            if (IsReservedAssetPath(assetPath))
            {
                errorMessage = $"Asset path is reserved for another domain: {assetPath}.";
                return false;
            }

            return TryValidatePersistentMainAssetIdentity(unityObject, assetPath, out errorMessage);
        }

        private static bool TryValidatePersistentMainAssetIdentity (
            UnityEngine.Object unityObject,
            string assetPath,
            out string errorMessage)
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset == null || mainAsset != unityObject)
            {
                errorMessage = $"Reference did not resolve to a main asset: {assetPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool IsProjectSettingsAssetPath (string assetPath)
        {
            return assetPath.StartsWith(ProjectSettingsRootPrefix, StringComparison.Ordinal);
        }

        private static bool IsReservedAssetPath (string assetPath)
        {
            return UnityAssetPathContract.IsNormalizedSceneAssetPath(assetPath)
                || UnityAssetPathContract.IsNormalizedPrefabAssetPath(assetPath);
        }
    }
}
