using System;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Maps public operation contract reference objects into Unity execution reference values. </summary>
    internal static class UnityObjectReferenceContractMapper
    {
        public static bool TryMap (
            ResolveSelectorArgs args,
            [NotNullWhen(true)] out ResolveSelector? selector,
            out string errorMessage)
        {
            return TryMapSelector(
                args.GlobalObjectId,
                args.AssetGuid,
                args.AssetPath,
                args.ProjectAssetPath,
                args.Scene,
                args.Prefab,
                args.HierarchyPath,
                args.ComponentType,
                out selector,
                out errorMessage);
        }

        public static bool TryMap (
            GameObjectReferenceArgs args,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias,
                args.GlobalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                args.Scene,
                args.Prefab,
                args.HierarchyPath,
                componentType: null,
                propertyPath,
                aliasReferences,
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            SceneGameObjectReferenceArgs args,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias,
                args.GlobalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                args.Scene,
                prefabPath: null,
                args.HierarchyPath,
                componentType: null,
                propertyPath,
                aliasReferences,
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            ComponentReferenceArgs args,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias,
                args.GlobalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                args.Scene,
                args.Prefab,
                args.HierarchyPath,
                args.ComponentType,
                propertyPath,
                aliasReferences,
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            AssetReferenceArgs args,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias,
                args.GlobalObjectId,
                args.AssetGuid,
                args.AssetPath,
                args.ProjectAssetPath,
                scenePath: null,
                prefabPath: null,
                hierarchyPath: null,
                componentType: null,
                propertyPath,
                aliasReferences,
                out reference,
                out errorMessage);
        }

        private static bool TryMapReference (
            UcliPlanAlias? alias,
            UnityGlobalObjectId? globalObjectId,
            Guid? assetGuid,
            UnityAssetPath? assetPath,
            ProjectSettingsAssetPath? projectAssetPath,
            SceneAssetPath? scenePath,
            PrefabAssetPath? prefabPath,
            UnityHierarchyPath? hierarchyPath,
            UnityComponentTypeId? componentType,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            reference = null;
            if (aliasReferences == null)
            {
                throw new ArgumentNullException(nameof(aliasReferences));
            }

            if (alias != null)
            {
                reference = UnityObjectReference.FromAlias(aliasReferences.Resolve(alias));
                errorMessage = string.Empty;
                return true;
            }

            if (!TryMapSelector(
                globalObjectId,
                assetGuid,
                assetPath,
                projectAssetPath,
                scenePath,
                prefabPath,
                hierarchyPath,
                componentType,
                out var selector,
                out errorMessage))
            {
                errorMessage = errorMessage.Replace("Operation 'args'", $"Operation '{propertyPath}'");
                return false;
            }

            reference = UnityObjectReference.FromSelector(selector);
            return true;
        }

        private static bool TryMapSelector (
            UnityGlobalObjectId? globalObjectId,
            Guid? assetGuid,
            UnityAssetPath? assetPath,
            ProjectSettingsAssetPath? projectAssetPath,
            SceneAssetPath? scenePath,
            PrefabAssetPath? prefabPath,
            UnityHierarchyPath? hierarchyPath,
            UnityComponentTypeId? componentType,
            [NotNullWhen(true)] out ResolveSelector? selector,
            out string errorMessage)
        {
            selector = null;
            if (globalObjectId != null)
            {
                selector = ResolveSelector.FromGlobalObjectId(globalObjectId);
                errorMessage = string.Empty;
                return true;
            }

            if (assetGuid != null)
            {
                selector = ResolveSelector.FromAssetGuid(assetGuid.Value);
                errorMessage = string.Empty;
                return true;
            }

            if (assetPath != null)
            {
                selector = ResolveSelector.FromAssetPath(assetPath);
                errorMessage = string.Empty;
                return true;
            }

            if (projectAssetPath != null)
            {
                selector = ResolveSelector.FromProjectAssetPath(projectAssetPath);
                errorMessage = string.Empty;
                return true;
            }

            if (scenePath != null && hierarchyPath != null)
            {
                selector = ResolveSelector.FromSceneHierarchy(scenePath, hierarchyPath, componentType);
                errorMessage = string.Empty;
                return true;
            }

            if (prefabPath != null && hierarchyPath != null)
            {
                selector = ResolveSelector.FromPrefabHierarchy(prefabPath, hierarchyPath, componentType);
                errorMessage = string.Empty;
                return true;
            }

            errorMessage =
                $"Operation 'args' must specify exactly one selector: '{IpcResolveSelectorPropertyNames.GlobalObjectId}', '{IpcResolveSelectorPropertyNames.AssetGuid}', '{IpcResolveSelectorPropertyNames.AssetPath}', '{IpcResolveSelectorPropertyNames.ProjectAssetPath}', '{IpcResolveSelectorPropertyNames.Scene}' + '{IpcResolveSelectorPropertyNames.HierarchyPath}', or '{IpcResolveSelectorPropertyNames.Prefab}' + '{IpcResolveSelectorPropertyNames.HierarchyPath}'.";
            return false;
        }
    }
}
