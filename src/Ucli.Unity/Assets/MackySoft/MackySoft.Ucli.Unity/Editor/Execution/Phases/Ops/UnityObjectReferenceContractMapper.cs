using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Maps public operation contract reference objects into Unity execution reference values. </summary>
    internal static class UnityObjectReferenceContractMapper
    {
        public static bool TryMap (
            UcliOperationContracts.ResolveSelectorArgs args,
            out ResolveSelector selector,
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
            UcliOperationContracts.GameObjectReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
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
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            UcliOperationContracts.SceneGameObjectReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
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
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            UcliOperationContracts.ComponentReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
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
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            UcliOperationContracts.AssetReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
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
                out reference,
                out errorMessage);
        }

        private static bool TryMapReference (
            string? alias,
            string? globalObjectId,
            string? assetGuid,
            string? assetPath,
            string? projectAssetPath,
            string? scenePath,
            string? prefabPath,
            string? hierarchyPath,
            string? componentType,
            string propertyPath,
            out UnityObjectReference reference,
            out string errorMessage)
        {
            reference = default;
            if (alias != null)
            {
                reference = UnityObjectReference.FromAlias(alias);
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
            string? globalObjectId,
            string? assetGuid,
            string? assetPath,
            string? projectAssetPath,
            string? scenePath,
            string? prefabPath,
            string? hierarchyPath,
            string? componentType,
            out ResolveSelector selector,
            out string errorMessage)
        {
            selector = default;
            if (globalObjectId != null)
            {
                selector = ResolveSelector.FromGlobalObjectId(globalObjectId);
                errorMessage = string.Empty;
                return true;
            }

            if (assetGuid != null)
            {
                selector = ResolveSelector.FromAssetGuid(assetGuid);
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
