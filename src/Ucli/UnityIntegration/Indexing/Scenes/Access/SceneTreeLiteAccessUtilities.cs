using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;

/// <summary> Provides shared helpers used by scene-tree-lite access services. </summary>
internal static class SceneTreeLiteAccessUtilities
{
    private const string AssetsRootPrefix = "Assets/";
    private const string SceneExtension = ".unity";

    /// <summary> Combines two fallback reason fragments into one user-facing sentence. </summary>
    public static string? CombineFallbackReasons (
        string? first,
        string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first} {second}";
    }

    /// <summary> Converts one freshness value to its wire-format literal. </summary>
    public static string DescribeFreshness (IndexFreshness freshness)
    {
        return freshness switch
        {
            IndexFreshness.Fresh => "fresh",
            IndexFreshness.Probable => "probable",
            IndexFreshness.Stale => "stale",
            _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "Unsupported index freshness."),
        };
    }

    /// <summary> Normalizes and validates one scene path input. </summary>
    public static bool TryNormalizeScenePath (
        string scenePath,
        out string normalizedScenePath,
        out string errorMessage)
    {
        normalizedScenePath = scenePath;
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            errorMessage = "Property 'path' must not be empty or whitespace.";
            return false;
        }

        if (StringValueValidator.HasOuterWhitespace(scenePath))
        {
            errorMessage = "Property 'path' must not contain leading or trailing whitespace.";
            return false;
        }

        normalizedScenePath = PathStringNormalizer.ToSlashSeparated(scenePath);
        if (HasUnsafePathSegments(normalizedScenePath))
        {
            errorMessage = "Property 'path' must be a project-relative path without '.' or '..' segments.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary> Returns whether the path can be persisted into scene-tree-lite read-index storage. </summary>
    public static bool IsLookupEligibleScenePath (string scenePath)
    {
        return !string.IsNullOrWhiteSpace(scenePath)
            && scenePath.StartsWith(AssetsRootPrefix, StringComparison.Ordinal)
            && scenePath.EndsWith(SceneExtension, StringComparison.Ordinal);
    }

    /// <summary> Ensures the current filesystem still contains the target scene asset under <c>Assets/</c>. </summary>
    public static bool TryEnsureCurrentAssetsSceneExists (
        string projectRootPath,
        string scenePath,
        out string errorMessage)
    {
        if (!TryResolveAbsoluteScenePath(projectRootPath, scenePath, out var absoluteScenePath, out errorMessage))
        {
            return false;
        }

        if (!File.Exists(absoluteScenePath))
        {
            errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary> Resolves one normalized project-relative scene path to its absolute filesystem path. </summary>
    private static bool TryResolveAbsoluteScenePath (
        string projectRootPath,
        string scenePath,
        out string absoluteScenePath,
        out string errorMessage)
    {
        absoluteScenePath = string.Empty;
        errorMessage = string.Empty;

        try
        {
            var projectRoot = Path.GetFullPath(projectRootPath);
            var candidatePath = Path.GetFullPath(Path.Combine(
                projectRoot,
                PathStringNormalizer.ToPlatformSeparated(scenePath)));
            if (!IsSameOrChildPath(projectRoot, candidatePath))
            {
                errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
                return false;
            }

            absoluteScenePath = candidatePath;
            return true;
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
            return false;
        }
    }

    /// <summary> Returns one tree snapshot trimmed to the requested depth. </summary>
    public static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> TrimToDepth (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        int? depth)
    {
        ArgumentNullException.ThrowIfNull(roots);

        if (depth == null)
        {
            return roots;
        }

        var trimmedRoots = new IndexSceneTreeLiteNodeJsonContract[roots.Count];
        for (var i = 0; i < roots.Count; i++)
        {
            trimmedRoots[i] = TrimNode(roots[i], currentDepth: 0, maxDepth: depth.Value);
        }

        return trimmedRoots;
    }

    private static IndexSceneTreeLiteNodeJsonContract TrimNode (
        IndexSceneTreeLiteNodeJsonContract node,
        int currentDepth,
        int maxDepth)
    {
        var children = currentDepth >= maxDepth
            ? Array.Empty<IndexSceneTreeLiteNodeJsonContract>()
            : TrimChildren(node.Children!, currentDepth + 1, maxDepth);
        return new IndexSceneTreeLiteNodeJsonContract(node.Name, node.GlobalObjectId, children);
    }

    private static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> TrimChildren (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> children,
        int childDepth,
        int maxDepth)
    {
        if (children.Count == 0)
        {
            return Array.Empty<IndexSceneTreeLiteNodeJsonContract>();
        }

        var trimmedChildren = new IndexSceneTreeLiteNodeJsonContract[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            trimmedChildren[i] = TrimNode(children[i], childDepth, maxDepth);
        }

        return trimmedChildren;
    }

    private static bool HasUnsafePathSegments (string scenePath)
    {
        if (Path.IsPathRooted(scenePath)
            || scenePath.StartsWith("/", StringComparison.Ordinal)
            || IsWindowsRootedPath(scenePath))
        {
            return true;
        }

        var segments = scenePath.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length == 0
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWindowsRootedPath (string scenePath)
    {
        return scenePath.Length >= 3
            && IsAsciiLetter(scenePath[0])
            && scenePath[1] == ':'
            && (scenePath[2] == '/' || scenePath[2] == '\\');
    }

    private static bool IsAsciiLetter (char value)
    {
        return value is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z');
    }

    private static bool IsSameOrChildPath (
        string parentPath,
        string childPath)
    {
        var normalizedParentPath = PathStringNormalizer.NormalizeCaseForCurrentPlatform(Path.GetFullPath(parentPath));
        var normalizedChildPath = PathStringNormalizer.NormalizeCaseForCurrentPlatform(Path.GetFullPath(childPath));
        if (string.Equals(normalizedParentPath, normalizedChildPath, StringComparison.Ordinal))
        {
            return true;
        }

        var parentPrefix = normalizedParentPath.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedParentPath
            : normalizedParentPath + Path.DirectorySeparatorChar;
        return normalizedChildPath.StartsWith(parentPrefix, StringComparison.Ordinal);
    }
}
