namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Provides current-platform filesystem path identity comparisons. </summary>
internal static class PathIdentity
{
    /// <summary> Determines whether two path values resolve to the same current-platform filesystem identity. </summary>
    /// <param name="leftPath"> The first path to compare. </param>
    /// <param name="rightPath"> The second path to compare. </param>
    /// <returns> <see langword="true" /> when both paths resolve to the same path; otherwise <see langword="false" />. </returns>
    public static bool IsSamePath (
        string leftPath,
        string rightPath)
    {
        var normalizedLeftPath = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(leftPath);
        var normalizedRightPath = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(rightPath);
        return IsSameNormalizedPath(normalizedLeftPath, normalizedRightPath);
    }

    /// <summary> Determines whether <paramref name="candidatePath" /> resolves to <paramref name="rootPath" /> or a descendant path. </summary>
    /// <param name="rootPath"> The root path used as the boundary. </param>
    /// <param name="candidatePath"> The candidate path to compare against the boundary. </param>
    /// <returns> <see langword="true" /> when the candidate is the same path or a descendant path; otherwise <see langword="false" />. </returns>
    public static bool IsSameOrChildPath (
        string rootPath,
        string candidatePath)
    {
        var normalizedRootPath = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(rootPath);
        var normalizedCandidatePath = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(candidatePath);
        return IsSameNormalizedPath(normalizedRootPath, normalizedCandidatePath)
            || IsChildPathCore(normalizedRootPath, normalizedCandidatePath);
    }

    /// <summary> Determines whether <paramref name="candidatePath" /> resolves to a descendant path of <paramref name="rootPath" />. </summary>
    /// <param name="rootPath"> The root path used as the boundary. </param>
    /// <param name="candidatePath"> The candidate path to compare against the boundary. </param>
    /// <returns> <see langword="true" /> when the candidate is a descendant path; otherwise <see langword="false" />. </returns>
    public static bool IsChildPath (
        string rootPath,
        string candidatePath)
    {
        var normalizedRootPath = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(rootPath);
        var normalizedCandidatePath = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(candidatePath);
        return !IsSameNormalizedPath(normalizedRootPath, normalizedCandidatePath)
            && IsChildPathCore(normalizedRootPath, normalizedCandidatePath);
    }

    private static bool IsSameNormalizedPath (
        string normalizedLeftPath,
        string normalizedRightPath)
    {
        return string.Equals(
            normalizedLeftPath,
            normalizedRightPath,
            PathStringNormalizer.CurrentPlatformPathComparison);
    }

    private static bool IsChildPathCore (
        string normalizedRootPath,
        string normalizedCandidatePath)
    {
        var rootPathPrefix = normalizedRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? normalizedRootPath
            : normalizedRootPath + Path.DirectorySeparatorChar;
        return normalizedCandidatePath.StartsWith(
            rootPathPrefix,
            PathStringNormalizer.CurrentPlatformPathComparison);
    }
}
