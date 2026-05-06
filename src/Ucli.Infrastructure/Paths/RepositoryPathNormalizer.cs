using System.Runtime.InteropServices;

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

        var targetPathResult = PathNormalizer.TryNormalizeFullPath(pathValue, repositoryRootResult.FullPath);
        if (!targetPathResult.IsSuccess)
        {
            return RepositoryPathNormalizationResult.Failure(
                targetPathResult.FailureKind,
                targetPathResult.DiagnosticMessage);
        }

        var repositoryRootFullPath = TrimTrailingDirectorySeparatorsUnlessRoot(repositoryRootResult.FullPath!);
        var targetFullPath = TrimTrailingDirectorySeparatorsUnlessRoot(targetPathResult.FullPath!);
        var comparisonRepositoryRoot = PathStringNormalizer.NormalizeCaseForCurrentPlatform(repositoryRootFullPath);
        var comparisonTargetPath = PathStringNormalizer.NormalizeCaseForCurrentPlatform(targetFullPath);

        if (string.Equals(comparisonTargetPath, comparisonRepositoryRoot, StringComparison.Ordinal))
        {
            return RepositoryPathNormalizationResult.Success(
                repositoryRootFullPath,
                RepositoryRootRelativePath);
        }

        if (!IsChildPath(comparisonRepositoryRoot, comparisonTargetPath))
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

    private static bool IsChildPath (
        string repositoryRoot,
        string targetPath)
    {
        var repositoryRootPrefix = repositoryRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? repositoryRoot
            : repositoryRoot + Path.DirectorySeparatorChar;
        return targetPath.StartsWith(repositoryRootPrefix, StringComparison.Ordinal);
    }

    private static string TrimTrailingDirectorySeparatorsUnlessRoot (string pathValue)
    {
        var platformPath = PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(pathValue);
        var pathRoot = Path.GetPathRoot(platformPath);
        if (!string.IsNullOrEmpty(pathRoot)
            && string.Equals(platformPath, pathRoot, GetPathComparison()))
        {
            return platformPath;
        }

        return PathStringNormalizer.TrimTrailingDirectorySeparators(platformPath);
    }

    private static StringComparison GetPathComparison ()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
