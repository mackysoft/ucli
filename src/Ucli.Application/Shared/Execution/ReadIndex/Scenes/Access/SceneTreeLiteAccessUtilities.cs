using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

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
        return ReadIndexAccessUtilities.CombineFallbackReasons(first, second);
    }

    /// <summary> Converts one freshness value to its wire-format literal. </summary>
    public static string DescribeFreshness (IndexFreshness freshness)
    {
        return ReadIndexAccessUtilities.DescribeFreshness(freshness);
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

        normalizedScenePath = ToSlashSeparated(scenePath);
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
        if (scenePath.StartsWith("/", StringComparison.Ordinal)
            || IsWindowsDriveQualifiedPath(scenePath))
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

    private static bool IsWindowsDriveQualifiedPath (string scenePath)
    {
        return scenePath.Length >= 2
            && IsAsciiLetter(scenePath[0])
            && scenePath[1] == ':';
    }

    private static bool IsAsciiLetter (char value)
    {
        return value is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z');
    }

    private static string ToSlashSeparated (string value)
    {
        return value.Replace('\\', '/');
    }
}
