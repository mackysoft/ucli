using System;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Maps public polymorphic reference contracts into Unity execution reference values. </summary>
    internal static class UnityObjectReferenceContractMapper
    {
        public static bool TryMap (
            ResolveSelectorArgs args,
            [NotNullWhen(true)] out ResolveSelector? selector,
            out string errorMessage)
        {
            return TryMapSelector(args, out selector, out errorMessage);
        }

        public static bool TryMap (
            UnityObjectReferenceArgs args,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            reference = null;
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (aliasReferences == null)
            {
                throw new ArgumentNullException(nameof(aliasReferences));
            }

            if (args is UcliAliasReferenceArgs aliasReference)
            {
                reference = UnityObjectReference.FromAlias(aliasReferences.Resolve(aliasReference.Alias));
                errorMessage = string.Empty;
                return true;
            }

            if (!TryMapSelector(args, out var selector, out errorMessage))
            {
                errorMessage = errorMessage.Replace("Operation 'args'", $"Operation '{propertyPath}'");
                return false;
            }

            reference = UnityObjectReference.FromSelector(selector);
            return true;
        }

        private static bool TryMapSelector (
            UnityObjectReferenceArgs args,
            [NotNullWhen(true)] out ResolveSelector? selector,
            out string errorMessage)
        {
            selector = null;
            switch (args)
            {
                case GlobalObjectIdReferenceArgs globalObjectId:
                    selector = ResolveSelector.FromGlobalObjectId(globalObjectId.GlobalObjectId);
                    break;
                case AssetGuidReferenceArgs assetGuid:
                    selector = ResolveSelector.FromAssetGuid(assetGuid.AssetGuid);
                    break;
                case AssetPathReferenceArgs assetPath:
                    selector = ResolveSelector.FromAssetPath(assetPath.AssetPath);
                    break;
                case ProjectAssetPathReferenceArgs projectAssetPath:
                    selector = ResolveSelector.FromProjectAssetPath(projectAssetPath.ProjectAssetPath);
                    break;
                case SceneHierarchyReferenceArgs sceneHierarchy:
                    selector = ResolveSelector.FromSceneHierarchy(
                        sceneHierarchy.Scene,
                        sceneHierarchy.HierarchyPath,
                        componentType: null);
                    break;
                case PrefabHierarchyReferenceArgs prefabHierarchy:
                    selector = ResolveSelector.FromPrefabHierarchy(
                        prefabHierarchy.Prefab,
                        prefabHierarchy.HierarchyPath,
                        componentType: null);
                    break;
                case SceneComponentReferenceArgs sceneComponent:
                    selector = ResolveSelector.FromSceneHierarchy(
                        sceneComponent.Scene,
                        sceneComponent.HierarchyPath,
                        sceneComponent.ComponentType);
                    break;
                case PrefabComponentReferenceArgs prefabComponent:
                    selector = ResolveSelector.FromPrefabHierarchy(
                        prefabComponent.Prefab,
                        prefabComponent.HierarchyPath,
                        prefabComponent.ComponentType);
                    break;
                default:
                    errorMessage = $"Operation 'args' contains an unsupported reference type: {args.GetType().FullName}.";
                    return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
