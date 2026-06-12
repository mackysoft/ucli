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

    /// <summary> Determines whether <paramref name="path" /> is a normalized path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedAssetsDescendantPath (string? path)
    {
        return path != null
            && IsNormalizedProjectRelativePath(path)
            && path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
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

    private static bool IsProjectRelativePathSyntax (string path)
    {
        return path.Length > 0
            && !path.StartsWith("/", StringComparison.Ordinal)
            && !IsWindowsDriveQualifiedPath(path)
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

    private static bool IsWindowsDriveQualifiedPath (string path)
    {
        return path.Length >= 2
            && IsAsciiLetter(path[0])
            && path[1] == ':';
    }

    private static bool IsAsciiLetter (char value)
    {
        return value is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z');
    }
}
