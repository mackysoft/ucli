using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines shared syntax rules for Unity project-relative asset paths. </summary>
public static class UnityAssetPathContract
{
    /// <summary> Gets the canonical Unity <c>Assets</c> root path. </summary>
    public const string AssetsRootPath = "Assets";

    /// <summary> Gets the canonical Unity <c>Assets/</c> descendant prefix. </summary>
    public const string AssetsRootPrefix = "Assets/";

    /// <summary> Gets the Unity scene asset extension. </summary>
    public const string SceneAssetExtension = ".unity";

    /// <summary> Gets the Unity prefab asset extension. </summary>
    public const string PrefabAssetExtension = ".prefab";

    /// <summary> Normalizes and validates one Unity project-relative path. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is project-relative and does not contain empty, <c>.</c>, or <c>..</c> segments; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeProjectRelativePath (
        string? path,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path)
            || StringValueValidator.HasOuterWhitespace(path))
        {
            return false;
        }

        var candidate = path.Replace('\\', '/');
        if (!IsProjectRelativePathSyntax(candidate))
        {
            return false;
        }

        normalizedPath = candidate;
        return true;
    }

    /// <summary> Determines whether <paramref name="path" /> is already normalized project-relative path text. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> satisfies the normalized project-relative path syntax; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedProjectRelativePath (string? path)
    {
        return path != null
            && !path.Contains('\\', StringComparison.Ordinal)
            && TryNormalizeProjectRelativePath(path, out var normalizedPath)
            && string.Equals(path, normalizedPath, StringComparison.Ordinal);
    }

    /// <summary> Determines whether <paramref name="path" /> is the normalized <c>Assets</c> root or a normalized path under it. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is <c>Assets</c> or an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedAssetsRootOrDescendantPath (string? path)
    {
        if (path == null || !IsNormalizedProjectRelativePath(path))
        {
            return false;
        }

        return string.Equals(path, AssetsRootPath, StringComparison.Ordinal)
            || path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one path that must identify the <c>Assets</c> root or a path under it. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to <c>Assets</c> or an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeAssetsRootOrDescendantPath (
        string? path,
        out string normalizedPath)
    {
        if (TryNormalizeProjectRelativePath(path, out normalizedPath)
            && IsNormalizedAssetsRootOrDescendantPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedAssetsDescendantPath (string? path)
    {
        return path != null
            && IsNormalizedProjectRelativePath(path)
            && path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one path that must identify an asset under <c>Assets/</c>. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeAssetsDescendantPath (
        string? path,
        out string normalizedPath)
    {
        if (TryNormalizeProjectRelativePath(path, out normalizedPath)
            && IsNormalizedAssetsDescendantPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized Unity scene asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is a normalized scene asset path; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedSceneAssetPath (string? path)
    {
        return path != null
            && IsNormalizedAssetsDescendantPath(path)
            && path.EndsWith(SceneAssetExtension, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one Unity scene asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to a Unity scene asset path; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeSceneAssetPath (
        string? path,
        out string normalizedPath)
    {
        if (TryNormalizeAssetsDescendantPath(path, out normalizedPath)
            && IsNormalizedSceneAssetPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized Unity prefab asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is a normalized prefab asset path; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedPrefabAssetPath (string? path)
    {
        return path != null
            && IsNormalizedAssetsDescendantPath(path)
            && path.EndsWith(PrefabAssetExtension, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one Unity prefab asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to a Unity prefab asset path; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizePrefabAssetPath (
        string? path,
        out string normalizedPath)
    {
        if (TryNormalizeAssetsDescendantPath(path, out normalizedPath)
            && IsNormalizedPrefabAssetPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    private static bool IsProjectRelativePathSyntax (string path)
    {
        return path.Length > 0
            && !path.StartsWith("/", StringComparison.Ordinal)
            && !path.Contains(':', StringComparison.Ordinal)
            && HasValidSegments(path);
    }

    private static bool HasValidSegments (string path)
    {
        var segmentStartIndex = 0;
        for (var i = 0; i <= path.Length; i++)
        {
            if (i < path.Length && path[i] != '/')
            {
                continue;
            }

            var segmentLength = i - segmentStartIndex;
            if (segmentLength == 0
                || IsCurrentDirectorySegment(path, segmentStartIndex, segmentLength)
                || IsParentDirectorySegment(path, segmentStartIndex, segmentLength))
            {
                return false;
            }

            segmentStartIndex = i + 1;
        }

        return true;
    }

    private static bool IsCurrentDirectorySegment (
        string path,
        int segmentStartIndex,
        int segmentLength)
    {
        return segmentLength == 1
            && path[segmentStartIndex] == '.';
    }

    private static bool IsParentDirectorySegment (
        string path,
        int segmentStartIndex,
        int segmentLength)
    {
        return segmentLength == 2
            && path[segmentStartIndex] == '.'
            && path[segmentStartIndex + 1] == '.';
    }

}
