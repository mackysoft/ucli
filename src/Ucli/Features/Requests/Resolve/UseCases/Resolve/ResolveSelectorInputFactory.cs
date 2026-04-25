using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Builds a normalized resolve selector from CLI selector flags. </summary>
internal static class ResolveSelectorInputFactory
{
    /// <summary> Attempts to create one selector from raw CLI option values. </summary>
    public static ResolveSelectorInputCreationResult Create (
        string? globalObjectId,
        string? assetGuid,
        string? assetPath,
        string? projectAssetPath,
        string? scene,
        string? hierarchyPath,
        string? componentType,
        string? prefab)
    {
        if (!TryNormalizeOptional(globalObjectId, "globalObjectId", out var normalizedGlobalObjectId, out var error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }
        if (!TryNormalizeOptional(assetGuid, "assetGuid", out var normalizedAssetGuid, out error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }
        if (!TryNormalizeOptional(assetPath, "assetPath", out var normalizedAssetPath, out error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }
        if (!TryNormalizeOptional(projectAssetPath, "projectAssetPath", out var normalizedProjectAssetPath, out error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }
        if (!TryNormalizeOptional(scene, "scene", out var normalizedScene, out error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }
        if (!TryNormalizeOptional(hierarchyPath, "hierarchyPath", out var normalizedHierarchyPath, out error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }
        if (!TryNormalizeOptional(componentType, "componentType", out var normalizedComponentType, out error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }
        if (!TryNormalizeOptional(prefab, "prefab", out var normalizedPrefab, out error))
        {
            return ResolveSelectorInputCreationResult.Failure(error!);
        }

        var selectorCount = CountSpecifiedSelectors(
            normalizedGlobalObjectId,
            normalizedAssetGuid,
            normalizedAssetPath,
            normalizedProjectAssetPath,
            normalizedScene,
            normalizedPrefab);
        if (selectorCount != 1)
        {
            return ResolveSelectorInputCreationResult.Failure(CreateExactlyOneSelectorError());
        }

        if (normalizedScene is not null)
        {
            if (normalizedHierarchyPath is null)
            {
                return ResolveSelectorInputCreationResult.Failure(CreateHierarchySelectorError());
            }

            return ResolveSelectorInputCreationResult.Success(new ResolveSelectorInput(
                Kind: normalizedComponentType is null ? ResolveSelectorKind.SceneHierarchyPath : ResolveSelectorKind.SceneComponent,
                GlobalObjectId: null,
                AssetGuid: null,
                AssetPath: null,
                ProjectAssetPath: null,
                Scene: normalizedScene,
                HierarchyPath: normalizedHierarchyPath,
                ComponentType: normalizedComponentType,
                Prefab: null));
        }

        if (normalizedPrefab is not null)
        {
            if (normalizedHierarchyPath is null)
            {
                return ResolveSelectorInputCreationResult.Failure(CreateHierarchySelectorError());
            }
            if (normalizedComponentType is not null)
            {
                return ResolveSelectorInputCreationResult.Failure(ExecutionError.InvalidArgument(
                    "Selector '--componentType' is supported only with '--scene --hierarchyPath'."));
            }

            return ResolveSelectorInputCreationResult.Success(new ResolveSelectorInput(
                Kind: ResolveSelectorKind.PrefabHierarchyPath,
                GlobalObjectId: null,
                AssetGuid: null,
                AssetPath: null,
                ProjectAssetPath: null,
                Scene: null,
                HierarchyPath: normalizedHierarchyPath,
                ComponentType: null,
                Prefab: normalizedPrefab));
        }

        if (normalizedHierarchyPath is not null || normalizedComponentType is not null)
        {
            return ResolveSelectorInputCreationResult.Failure(CreateHierarchySelectorError());
        }

        if (normalizedGlobalObjectId is not null)
        {
            return CreateScalarSelector(ResolveSelectorKind.GlobalObjectId, normalizedGlobalObjectId, null, null, null);
        }
        if (normalizedAssetGuid is not null)
        {
            return CreateScalarSelector(ResolveSelectorKind.AssetGuid, null, normalizedAssetGuid, null, null);
        }
        if (normalizedAssetPath is not null)
        {
            return CreateScalarSelector(ResolveSelectorKind.AssetPath, null, null, normalizedAssetPath, null);
        }

        return CreateScalarSelector(ResolveSelectorKind.ProjectAssetPath, null, null, null, normalizedProjectAssetPath);
    }

    private static ResolveSelectorInputCreationResult CreateScalarSelector (
        ResolveSelectorKind kind,
        string? globalObjectId,
        string? assetGuid,
        string? assetPath,
        string? projectAssetPath)
    {
        return ResolveSelectorInputCreationResult.Success(new ResolveSelectorInput(
            Kind: kind,
            GlobalObjectId: globalObjectId,
            AssetGuid: assetGuid,
            AssetPath: assetPath,
            ProjectAssetPath: projectAssetPath,
            Scene: null,
            HierarchyPath: null,
            ComponentType: null,
            Prefab: null));
    }

    private static bool TryNormalizeOptional (
        string? value,
        string optionName,
        out string? normalizedValue,
        out ExecutionError? error)
    {
        normalizedValue = null;
        error = null;
        if (value is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            error = ExecutionError.InvalidArgument($"Selector '--{optionName}' must not be empty or whitespace.");
            return false;
        }

        var trimmedValue = value.Trim();
        if (!string.Equals(value, trimmedValue, StringComparison.Ordinal))
        {
            error = ExecutionError.InvalidArgument($"Selector '--{optionName}' must not contain leading or trailing whitespace.");
            return false;
        }

        normalizedValue = value;
        return true;
    }

    private static int CountSpecifiedSelectors (
        string? globalObjectId,
        string? assetGuid,
        string? assetPath,
        string? projectAssetPath,
        string? scene,
        string? prefab)
    {
        var count = 0;
        count += globalObjectId is null ? 0 : 1;
        count += assetGuid is null ? 0 : 1;
        count += assetPath is null ? 0 : 1;
        count += projectAssetPath is null ? 0 : 1;
        count += scene is null ? 0 : 1;
        count += prefab is null ? 0 : 1;
        return count;
    }

    private static ExecutionError CreateExactlyOneSelectorError ()
    {
        return ExecutionError.InvalidArgument(
            "ucli resolve requires exactly one selector: --globalObjectId, --assetGuid, --assetPath, --projectAssetPath, --scene --hierarchyPath, or --prefab --hierarchyPath.");
    }

    private static ExecutionError CreateHierarchySelectorError ()
    {
        return ExecutionError.InvalidArgument(
            "Hierarchy selectors require either '--scene --hierarchyPath' or '--prefab --hierarchyPath'.");
    }
}