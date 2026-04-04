using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers shared by prefab-domain phase operations. </summary>
    internal static class PrefabOperationUtilities
    {
        private const string AssetsRootPrefix = "Assets/";

        private const string PrefabExtension = ".prefab";

        /// <summary> Validates that the specified path resolves to a prefab asset. </summary>
        /// <param name="prefabPath"> The prefab path. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when prefab asset exists; otherwise <see langword="false" />. </returns>
        public static bool TryEnsurePrefabAssetExists (
            string prefabPath,
            out string errorMessage)
        {
            if (!TryValidatePrefabAssetPathFormat(prefabPath, out errorMessage))
            {
                return false;
            }

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                errorMessage = $"Prefab path could not be resolved to a prefab asset: {prefabPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Validates that the specified path can be used to create a new prefab asset. </summary>
        /// <param name="prefabPath"> The prefab path. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when the path is creatable; otherwise <see langword="false" />. </returns>
        public static bool TryEnsurePrefabAssetCanBeCreated (
            string prefabPath,
            out string errorMessage)
        {
            if (!TryValidatePrefabAssetPathFormat(prefabPath, out errorMessage))
            {
                return false;
            }

            if (AssetDatabase.LoadMainAssetAtPath(prefabPath) != null)
            {
                errorMessage = $"Prefab asset already exists: {prefabPath}.";
                return false;
            }

            var directoryPath = Path.GetDirectoryName(prefabPath);
            if (string.IsNullOrWhiteSpace(directoryPath)
                || !AssetDatabase.IsValidFolder(directoryPath))
            {
                errorMessage = $"Prefab path directory does not exist: {prefabPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
        /// <summary> Gets one currently opened prefab stage by asset path. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="prefabStage"> The opened prefab stage when successful. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when the target prefab stage is opened; otherwise <see langword="false" />. </returns>
        public static bool TryGetOpenedPrefabStage (
            string prefabPath,
            out PrefabStage? prefabStage,
            out string errorMessage)
        {
            prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                errorMessage = $"Prefab is not opened: {prefabPath}. Use 'ucli.prefab.open' first.";
                return false;
            }

            if (!string.Equals(prefabStage.assetPath, prefabPath, StringComparison.Ordinal))
            {
                errorMessage = $"Opened prefab does not match requested path: {prefabPath}.";
                prefabStage = null;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Opens one prefab stage by asset path. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="prefabStage"> The opened prefab stage when successful. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when the prefab stage was opened successfully; otherwise <see langword="false" />. </returns>
        public static bool TryOpenPrefabStage (
            string prefabPath,
            out PrefabStage? prefabStage,
            out string errorMessage)
        {
            if (TryGetOpenedPrefabStage(prefabPath, out prefabStage, out errorMessage))
            {
                return true;
            }

            try
            {
                prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            }
            catch (Exception exception)
            {
                prefabStage = null;
                errorMessage = $"Prefab could not be opened: {prefabPath}. {exception.Message}";
                return false;
            }

            if (prefabStage == null
                || !string.Equals(prefabStage.assetPath, prefabPath, StringComparison.Ordinal))
            {
                errorMessage = $"Prefab could not be opened: {prefabPath}.";
                prefabStage = null;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Gets one request-local prefab-contents root for plan execution, loading contents when needed. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="prefabContentsRoot"> The prefab-contents root when successful. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when prefab contents were obtained successfully; otherwise <see langword="false" />. </returns>
        public static bool TryGetOrLoadTemporaryPrefabContentsRoot (
            string prefabPath,
            OperationExecutionContext executionContext,
            out GameObject? prefabContentsRoot,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out prefabContentsRoot))
            {
                errorMessage = string.Empty;
                return true;
            }

            try
            {
                prefabContentsRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (Exception exception)
            {
                prefabContentsRoot = null;
                errorMessage = $"Prefab contents could not be loaded: {prefabPath}. {exception.Message}";
                return false;
            }

            if (prefabContentsRoot == null)
            {
                errorMessage = $"Prefab contents could not be loaded: {prefabPath}.";
                return false;
            }

            executionContext.TrackTemporaryPrefabContentsRoot(prefabPath, prefabContentsRoot);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryValidatePrefabAssetPathFormat (
            string prefabPath,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                errorMessage = "Prefab path must not be null, empty, or whitespace.";
                return false;
            }

            if (!prefabPath.StartsWith(AssetsRootPrefix, StringComparison.Ordinal))
            {
                errorMessage = $"Prefab path must start with '{AssetsRootPrefix}': {prefabPath}.";
                return false;
            }

            if (!prefabPath.EndsWith(PrefabExtension, StringComparison.Ordinal))
            {
                errorMessage = $"Prefab path must end with '{PrefabExtension}': {prefabPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
