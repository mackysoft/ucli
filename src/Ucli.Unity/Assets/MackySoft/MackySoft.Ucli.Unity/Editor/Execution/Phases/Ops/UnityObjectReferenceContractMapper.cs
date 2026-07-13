using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Maps public operation contract reference objects into Unity execution reference values. </summary>
    internal static class UnityObjectReferenceContractMapper
    {
        public static bool TryMap (
            ResolveSelectorArgs args,
            out ResolveSelector selector,
            out string errorMessage)
        {
            return TryMapSelector(
                args.GlobalObjectId,
                args.AssetGuid?.Value,
                args.AssetPath?.Value,
                args.ProjectAssetPath?.Value,
                args.Scene?.Value,
                args.Prefab?.Value,
                args.HierarchyPath?.Value,
                args.ComponentType?.Value,
                out selector,
                out errorMessage);
        }

        public static bool TryMap (
            GameObjectReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias?.Value,
                args.GlobalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                args.Scene?.Value,
                args.Prefab?.Value,
                args.HierarchyPath?.Value,
                componentType: null,
                propertyPath,
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            SceneGameObjectReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias?.Value,
                args.GlobalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                args.Scene?.Value,
                prefabPath: null,
                args.HierarchyPath?.Value,
                componentType: null,
                propertyPath,
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            ComponentReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias?.Value,
                args.GlobalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                args.Scene?.Value,
                args.Prefab?.Value,
                args.HierarchyPath?.Value,
                args.ComponentType?.Value,
                propertyPath,
                out reference,
                out errorMessage);
        }

        public static bool TryMap (
            AssetReferenceArgs args,
            string propertyPath,
            out UnityObjectReference reference,
            out string errorMessage)
        {
            return TryMapReference(
                args.Alias?.Value,
                args.GlobalObjectId,
                args.AssetGuid?.Value,
                args.AssetPath?.Value,
                args.ProjectAssetPath?.Value,
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
            UnityGlobalObjectId? globalObjectId,
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
            UnityGlobalObjectId? globalObjectId,
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
