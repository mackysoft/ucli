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
        var absoluteScenePath = ResolveAbsoluteScenePath(projectRootPath, scenePath);
        if (!File.Exists(absoluteScenePath))
        {
            errorMessage = $"Scene path could not be resolved to a scene asset: {scenePath}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary> Resolves one normalized project-relative scene path to its absolute filesystem path. </summary>
    public static string ResolveAbsoluteScenePath (
        string projectRootPath,
        string scenePath)
    {
        return Path.Combine(
            Path.GetFullPath(projectRootPath),
            PathStringNormalizer.ToPlatformSeparated(scenePath));
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
}
