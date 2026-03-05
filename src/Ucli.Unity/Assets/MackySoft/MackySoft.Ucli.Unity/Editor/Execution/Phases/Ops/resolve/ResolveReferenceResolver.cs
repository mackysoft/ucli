using System;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves parsed selectors to GlobalObjectId-normalized references. </summary>
    internal static class ResolveReferenceResolver
    {
        /// <summary> Determines whether GlobalObjectId text is syntactically valid. </summary>
        /// <param name="globalObjectIdText"> The GlobalObjectId text value. </param>
        /// <returns> <see langword="true" /> when text is syntactically valid; otherwise <see langword="false" />. </returns>
        public static bool IsValidGlobalObjectIdText (string globalObjectIdText)
        {
            return GlobalObjectId.TryParse(globalObjectIdText, out _);
        }

        /// <summary> Tries to resolve one selector to a GlobalObjectId-normalized reference. </summary>
        /// <param name="selector"> The parsed selector. </param>
        /// <param name="resolvedReference"> The resolved reference when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryResolve (
            ResolveSelector selector,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            switch (selector.Kind)
            {
                case ResolveSelectorKind.GlobalObjectId:
                    return TryResolveFromGlobalObjectId(selector.GlobalObjectId!, out resolvedReference, out errorMessage);

                case ResolveSelectorKind.AssetGuid:
                    return TryResolveFromAssetGuid(selector.AssetGuid!, out resolvedReference, out errorMessage);

                case ResolveSelectorKind.AssetPath:
                    return TryResolveFromAssetPath(selector.AssetPath!, out resolvedReference, out errorMessage);

                case ResolveSelectorKind.SceneHierarchyPath:
                    return TryResolveFromSceneHierarchyPath(
                        selector.ScenePath!,
                        selector.HierarchyPath!,
                        out resolvedReference,
                        out errorMessage);

                default:
                    resolvedReference = null;
                    errorMessage = "Operation selector kind is not supported.";
                    return false;
            }
        }

        /// <summary> Tries to resolve one GlobalObjectId selector. </summary>
        /// <param name="globalObjectIdText"> The GlobalObjectId text value. </param>
        /// <param name="resolvedReference"> The resolved reference when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when selector resolves successfully; otherwise <see langword="false" />. </returns>
        private static bool TryResolveFromGlobalObjectId (
            string globalObjectIdText,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            resolvedReference = null;
            if (!GlobalObjectId.TryParse(globalObjectIdText, out var globalObjectId))
            {
                errorMessage = $"'{ResolveSelectorPropertyNames.GlobalObjectId}' must be a valid GlobalObjectId string.";
                return false;
            }

            var unityObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            if (unityObject == null)
            {
                errorMessage = $"GlobalObjectId is not resolvable in current project state: {globalObjectIdText}.";
                return false;
            }

            resolvedReference = CreateResolvedReference(unityObject);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to resolve one asset-guid selector. </summary>
        /// <param name="assetGuid"> The asset GUID value. </param>
        /// <param name="resolvedReference"> The resolved reference when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when selector resolves successfully; otherwise <see langword="false" />. </returns>
        private static bool TryResolveFromAssetGuid (
            string assetGuid,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            resolvedReference = null;
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                errorMessage = $"Asset GUID could not be resolved: {assetGuid}.";
                return false;
            }

            return TryResolveFromAssetPath(assetPath, out resolvedReference, out errorMessage);
        }

        /// <summary> Tries to resolve one asset-path selector. </summary>
        /// <param name="assetPath"> The asset path value. </param>
        /// <param name="resolvedReference"> The resolved reference when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when selector resolves successfully; otherwise <see langword="false" />. </returns>
        private static bool TryResolveFromAssetPath (
            string assetPath,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            resolvedReference = null;
            var unityObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (unityObject == null)
            {
                errorMessage = $"Asset path could not be resolved to a main asset: {assetPath}.";
                return false;
            }

            resolvedReference = CreateResolvedReference(unityObject);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to resolve one scene + hierarchy-path selector. </summary>
        /// <param name="scenePath"> The scene path value. </param>
        /// <param name="hierarchyPath"> The hierarchy path value. </param>
        /// <param name="resolvedReference"> The resolved reference when successful. </param>
        /// <param name="errorMessage"> The resolution error message when failed. </param>
        /// <returns> <see langword="true" /> when selector resolves successfully; otherwise <see langword="false" />. </returns>
        private static bool TryResolveFromSceneHierarchyPath (
            string scenePath,
            string hierarchyPath,
            out ResolvedReference? resolvedReference,
            out string errorMessage)
        {
            resolvedReference = null;
            if (!SceneHierarchyPathResolver.TryResolveLoadedSceneObject(scenePath, hierarchyPath, out var gameObject, out errorMessage))
            {
                return false;
            }

            resolvedReference = CreateResolvedReference(gameObject!);
            return true;
        }

        /// <summary> Converts one Unity object into a GlobalObjectId-normalized reference. </summary>
        /// <param name="unityObject"> The Unity object to normalize. </param>
        /// <returns> The normalized resolved reference. </returns>
        private static ResolvedReference CreateResolvedReference (UnityEngine.Object unityObject)
        {
            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            return new ResolvedReference(globalObjectId.ToString());
        }
    }
}
