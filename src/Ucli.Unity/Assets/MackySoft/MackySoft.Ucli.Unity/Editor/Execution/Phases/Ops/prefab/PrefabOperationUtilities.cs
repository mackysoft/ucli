using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers shared by prefab-domain phase operations. </summary>
    internal static class PrefabOperationUtilities
    {
        /// <summary> Validates that the specified path resolves to a prefab asset. </summary>
        /// <param name="prefabPath"> The normalized prefab path. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when prefab asset exists; otherwise <see langword="false" />. </returns>
        public static bool TryEnsurePrefabAssetExists (
            PrefabAssetPath prefabPath,
            out string errorMessage)
        {
            if (prefabPath == null)
            {
                throw new ArgumentNullException(nameof(prefabPath));
            }

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath.Value);
            if (prefabAsset == null)
            {
                errorMessage = $"Prefab path could not be resolved to a prefab asset: {prefabPath.Value}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Validates that the specified path can be used to create a new prefab asset. </summary>
        /// <param name="prefabPath"> The normalized prefab path. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when the path is creatable; otherwise <see langword="false" />. </returns>
        public static bool TryEnsurePrefabAssetCanBeCreated (
            PrefabAssetPath prefabPath,
            out string errorMessage)
        {
            if (prefabPath == null)
            {
                throw new ArgumentNullException(nameof(prefabPath));
            }

            if (AssetDatabase.LoadMainAssetAtPath(prefabPath.Value) != null)
            {
                errorMessage = $"Prefab asset already exists: {prefabPath.Value}.";
                return false;
            }

            var directoryPath = Path.GetDirectoryName(prefabPath.Value);
            if (string.IsNullOrWhiteSpace(directoryPath)
                || !AssetDatabase.IsValidFolder(directoryPath))
            {
                errorMessage = $"Prefab path directory does not exist: {prefabPath.Value}.";
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
            [NotNullWhen(true)] out PrefabStage? prefabStage,
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
            [NotNullWhen(true)] out PrefabStage? prefabStage,
            out string errorMessage)
        {
            if (TryGetOpenedPrefabStage(prefabPath, out prefabStage, out errorMessage))
            {
                return true;
            }

            if (!TryEnsureCanOpenPrefabStage(prefabPath, out errorMessage))
            {
                prefabStage = null;
                return false;
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

        /// <summary> Validates that opening one live prefab stage will not be blocked by dirty live editor state. </summary>
        /// <param name="prefabPath"> The target prefab path. </param>
        /// <param name="errorMessage"> The validation error message when blocked. </param>
        /// <returns> <see langword="true" /> when the prefab stage can be opened without dirty-state blockers; otherwise <see langword="false" />. </returns>
        public static bool TryEnsureCanOpenPrefabStage (
            string prefabPath,
            out string errorMessage)
        {
            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentPrefabStage != null
                && currentPrefabStage.scene.isDirty)
            {
                errorMessage = $"Dirty prefab stage blocks opening prefab '{prefabPath}': {currentPrefabStage.assetPath}.";
                return false;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var loadedScene = SceneManager.GetSceneAt(i);
                if (!loadedScene.IsValid()
                    || !loadedScene.isLoaded
                    || EditorSceneManager.IsPreviewScene(loadedScene)
                    || !loadedScene.isDirty)
                {
                    continue;
                }

                errorMessage = $"Dirty loaded scene blocks opening prefab '{prefabPath}': {CreateSceneDisplayName(loadedScene)}.";
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
            [NotNullWhen(true)] out GameObject? prefabContentsRoot,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out prefabContentsRoot)
                && prefabContentsRoot != null)
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

        private static string CreateSceneDisplayName (Scene scene)
        {
            return string.IsNullOrWhiteSpace(scene.path)
                ? scene.name
                : scene.path;
        }

    }
}
