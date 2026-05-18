using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Assurance.Verify;

/// <summary> Resolves verify input files under the repository root boundary. </summary>
internal static class VerifyRepositoryFilePathResolver
{
    /// <summary> Resolves one repository-local file path and rejects symlink traversal. </summary>
    public static bool TryResolve (
        string repositoryRoot,
        string path,
        out string fullPath,
        out string repositoryRelativePath,
        out string diagnosticMessage)
    {
        fullPath = string.Empty;
        repositoryRelativePath = string.Empty;
        diagnosticMessage = string.Empty;

        var normalizationResult = RepositoryPathNormalizer.TryNormalize(repositoryRoot, path);
        if (!normalizationResult.IsSuccess)
        {
            diagnosticMessage = normalizationResult.DiagnosticMessage;
            return false;
        }

        fullPath = normalizationResult.FullPath!;
        repositoryRelativePath = normalizationResult.RepositoryRelativeSlashPath!;
        if (ContainsReparsePoint(normalizationResult.FullPath!, normalizationResult.RepositoryRelativeSlashPath!))
        {
            diagnosticMessage = "Repository-local verify input paths must not traverse symbolic links.";
            return false;
        }

        return true;
    }

    private static bool ContainsReparsePoint (
        string fullPath,
        string repositoryRelativePath)
    {
        try
        {
            if (string.Equals(repositoryRelativePath, ".", StringComparison.Ordinal))
            {
                return false;
            }

            var currentPath = fullPath;
            var remainingSegments = repositoryRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
            while (remainingSegments > 0)
            {
                if (Path.Exists(currentPath)
                    && File.GetAttributes(currentPath).HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }

                currentPath = Path.GetDirectoryName(currentPath) ?? string.Empty;
                if (string.IsNullOrEmpty(currentPath))
                {
                    return false;
                }

                remainingSegments--;
            }

            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return true;
        }
    }
}
