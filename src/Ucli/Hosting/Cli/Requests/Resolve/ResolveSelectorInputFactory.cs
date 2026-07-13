using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Builds a normalized resolve selector from raw selector option values. </summary>
internal static class ResolveSelectorInputFactory
{
    /// <summary> Attempts to create one selector from raw option values. </summary>
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

            if (normalizedComponentType is null)
            {
                return ResolveSelectorInputCreationResult.Success(
                    new ResolveSceneHierarchySelectorInput(normalizedScene, normalizedHierarchyPath));
            }

            return ResolveSelectorInputCreationResult.Success(
                new ResolveSceneComponentSelectorInput(normalizedScene, normalizedHierarchyPath, normalizedComponentType));
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

            return ResolveSelectorInputCreationResult.Success(
                new ResolvePrefabHierarchySelectorInput(normalizedPrefab, normalizedHierarchyPath));
        }

        if (normalizedHierarchyPath is not null || normalizedComponentType is not null)
        {
            return ResolveSelectorInputCreationResult.Failure(CreateHierarchySelectorError());
        }

        if (normalizedGlobalObjectId is not null)
        {
            if (!UnityGlobalObjectId.TryParse(normalizedGlobalObjectId, out var typedGlobalObjectId))
            {
                return ResolveSelectorInputCreationResult.Failure(ExecutionError.InvalidArgument(
                    "Selector '--globalObjectId' must be a supported non-null Unity GlobalObjectId."));
            }

            return ResolveSelectorInputCreationResult.Success(new ResolveGlobalObjectIdSelectorInput(typedGlobalObjectId));
        }
        if (normalizedAssetGuid is not null)
        {
            if (!UnityAssetGuid.TryParse(normalizedAssetGuid, out var typedAssetGuid))
            {
                return ResolveSelectorInputCreationResult.Failure(ExecutionError.InvalidArgument(
                    "Selector '--assetGuid' must be a non-zero 32-character hexadecimal Unity asset GUID."));
            }

            return ResolveSelectorInputCreationResult.Success(new ResolveAssetGuidSelectorInput(typedAssetGuid));
        }
        if (normalizedAssetPath is not null)
        {
            return ResolveSelectorInputCreationResult.Success(new ResolveAssetPathSelectorInput(normalizedAssetPath));
        }

        if (normalizedProjectAssetPath is not null)
        {
            return ResolveSelectorInputCreationResult.Success(new ResolveProjectAssetPathSelectorInput(normalizedProjectAssetPath));
        }

        throw new InvalidOperationException("Exactly one resolve selector was expected after validation.");
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
