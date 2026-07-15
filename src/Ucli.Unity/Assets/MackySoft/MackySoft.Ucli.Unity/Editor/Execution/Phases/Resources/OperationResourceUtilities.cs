using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers for owner-resource resolution and touched-resource creation. </summary>
    internal static class OperationResourceUtilities
    {
        /// <summary> Creates one touched entry from the specified owner resource. </summary>
        /// <param name="resource"> The owner resource. </param>
        /// <returns> The touched entry. </returns>
        public static OperationTouch CreateTouch (OperationResource resource)
        {
            return new OperationTouch(
                kind: resource.Kind,
                path: resource.Path,
                assetGuid: ResolveAssetGuid(resource));
        }

        /// <summary> Creates one stable touched-resource list from owner resources. </summary>
        /// <param name="resources"> The owner resources. </param>
        /// <returns> The stable touched-resource list. </returns>
        public static IReadOnlyList<OperationTouch> CreateTouches (params OperationResource[] resources)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            var touchesByPath = new SortedDictionary<string, OperationTouch>(StringComparer.Ordinal);
            for (var index = 0; index < resources.Length; index++)
            {
                var touch = CreateTouch(resources[index]);
                if (touchesByPath.TryGetValue(touch.Path, out var existing)
                    && existing.AssetGuid.HasValue)
                {
                    continue;
                }

                touchesByPath[touch.Path] = touch;
            }

            var touches = new OperationTouch[touchesByPath.Count];
            var touchIndex = 0;
            foreach (var pair in touchesByPath)
            {
                touches[touchIndex] = pair.Value;
                touchIndex++;
            }

            return touches;
        }

        /// <summary> Resolves one owner resource from a live GameObject. </summary>
        /// <param name="gameObject"> The source GameObject. </param>
        /// <param name="resource"> The resolved owner resource when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when owner resource was resolved; otherwise <see langword="false" />. </returns>
        public static bool TryResolveOwnerResource (
            GameObject gameObject,
            OperationExecutionContext executionContext,
            out OperationResource resource,
            out string errorMessage)
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (executionContext.TryResolveTemporaryPrefabPath(gameObject, out var temporaryPrefabPath))
            {
                resource = new OperationResource(UcliTouchedResourceKind.Prefab, temporaryPrefabPath);
                errorMessage = string.Empty;
                return true;
            }

            var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (prefabStage != null
                && !string.IsNullOrWhiteSpace(prefabStage.assetPath))
            {
                resource = new OperationResource(UcliTouchedResourceKind.Prefab, prefabStage.assetPath);
                errorMessage = string.Empty;
                return true;
            }

            var scene = gameObject.scene;
            if (scene.IsValid()
                && scene.isLoaded
                && executionContext.TryResolveTemporaryScenePath(scene, out var previewScenePath))
            {
                resource = new OperationResource(UcliTouchedResourceKind.Scene, previewScenePath);
                errorMessage = string.Empty;
                return true;
            }

            if (scene.IsValid()
                && scene.isLoaded
                && !string.IsNullOrWhiteSpace(scene.path))
            {
                resource = new OperationResource(UcliTouchedResourceKind.Scene, scene.path);
                errorMessage = string.Empty;
                return true;
            }

            resource = default;
            errorMessage = "GameObject is not part of a loaded scene, tracked prefab preview, or opened prefab.";
            return false;
        }

        /// <summary> Resolves one owner resource from a live Component. </summary>
        /// <param name="component"> The source component. </param>
        /// <param name="resource"> The resolved owner resource when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when owner resource was resolved; otherwise <see langword="false" />. </returns>
        public static bool TryResolveOwnerResource (
            Component component,
            OperationExecutionContext executionContext,
            out OperationResource resource,
            out string errorMessage)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            return TryResolveOwnerResource(component.gameObject, executionContext, out resource, out errorMessage);
        }

        private static Guid? ResolveAssetGuid (OperationResource resource)
        {
            if (resource.Kind == UcliTouchedResourceKind.ProjectSettings)
            {
                return null;
            }

            var assetGuidText = AssetDatabase.AssetPathToGUID(resource.Path);
            if (string.IsNullOrEmpty(assetGuidText))
            {
                return null;
            }

            if (!Guid.TryParseExact(assetGuidText, "N", out var assetGuid)
                || assetGuid == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"Unity returned an invalid asset GUID for '{resource.Path}'.");
            }

            return assetGuid;
        }
    }
}
