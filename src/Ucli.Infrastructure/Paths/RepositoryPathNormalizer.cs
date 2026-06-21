namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Normalizes paths against a repository root boundary. </summary>
internal static class RepositoryPathNormalizer
{
    private const string RepositoryRootRelativePath = ".";

    /// <summary> Attempts to normalize one path value under the repository root boundary. </summary>
    /// <param name="repositoryRoot"> The repository root path used as the boundary and relative base path. </param>
    /// <param name="pathValue"> The path value to normalize. Relative values are resolved from <paramref name="repositoryRoot" />. </param>
    /// <returns> The repository path normalization result. </returns>
    public static RepositoryPathNormalizationResult TryNormalize (
        string? repositoryRoot,
        string? pathValue)
    {
        var repositoryRootResult = PathNormalizer.TryNormalizeFullPath(repositoryRoot);
        if (!repositoryRootResult.IsSuccess)
        {
            return RepositoryPathNormalizationResult.Failure(
                repositoryRootResult.FailureKind,
                $"Repository root is invalid. {repositoryRootResult.DiagnosticMessage}");
        }

        var pathValueForResolution = pathValue is null
            ? null
            : PathStringNormalizer.ToPlatformSeparated(pathValue);
        var targetPathResult = PathNormalizer.TryNormalizeFullPath(pathValueForResolution, repositoryRootResult.FullPath);
        if (!targetPathResult.IsSuccess)
        {
            return RepositoryPathNormalizationResult.Failure(
                targetPathResult.FailureKind,
                targetPathResult.DiagnosticMessage);
        }

        var repositoryRootFullPath = TrimTrailingDirectorySeparatorsUnlessRoot(repositoryRootResult.FullPath!);
        var targetFullPath = TrimTrailingDirectorySeparatorsUnlessRoot(targetPathResult.FullPath!);
        if (PathIdentity.IsSamePath(repositoryRootFullPath, targetFullPath))
        {
            return RepositoryPathNormalizationResult.Success(
                repositoryRootFullPath,
                RepositoryRootRelativePath);
        }

        if (!PathIdentity.IsChildPath(repositoryRootFullPath, targetFullPath))
        {
            return RepositoryPathNormalizationResult.Failure(
                PathNormalizationFailureKind.OutsideRepositoryRoot,
                "Path must be under the repository root.");
        }

        var repositoryRelativePath = Path.GetRelativePath(repositoryRootFullPath, targetFullPath);
        var repositoryRelativeSlashPath = PathStringNormalizer.ToSlashSeparated(
            PathStringNormalizer.TrimTrailingDirectorySeparators(repositoryRelativePath));
        if (string.IsNullOrEmpty(repositoryRelativeSlashPath))
        {
            repositoryRelativeSlashPath = RepositoryRootRelativePath;
        }

        return RepositoryPathNormalizationResult.Success(
            targetFullPath,
            repositoryRelativeSlashPath);
    }

    private static string TrimTrailingDirectorySeparatorsUnlessRoot (string pathValue)
    {
        var platformPath = PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(pathValue);
        if (PathStringNormalizer.IsPathRoot(platformPath))
        {
            return platformPath;
        }

        return PathStringNormalizer.TrimTrailingDirectorySeparators(platformPath);
    }
}
