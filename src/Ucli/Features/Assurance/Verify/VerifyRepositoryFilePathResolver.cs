using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Features.Assurance.Verify;

/// <summary> Resolves verify input files under the repository root boundary. </summary>
internal static class VerifyRepositoryFilePathResolver
{
    /// <summary> Resolves one repository-local file path and rejects symlink traversal. </summary>
    public static bool TryResolve (
        AbsolutePath repositoryRoot,
        string path,
        [NotNullWhen(true)] out ContainedPath? resolvedPath,
        out string diagnosticMessage)
    {
        resolvedPath = null;
        diagnosticMessage = string.Empty;

        if (!ContainedPath.TryResolve(repositoryRoot, path, out resolvedPath, out var failure))
        {
            diagnosticMessage = failure.Message;
            return false;
        }

        if (ContainsReparsePoint(resolvedPath))
        {
            resolvedPath = null;
            diagnosticMessage = "Repository-local verify input paths must not traverse symbolic links.";
            return false;
        }

        return true;
    }

    private static bool ContainsReparsePoint (
        ContainedPath path)
    {
        try
        {
            if (path.RelativePath.IsRoot)
            {
                return false;
            }

            var currentPath = path.Target;
            while (!currentPath.IsSameAs(path.BoundaryRoot))
            {
                if (Path.Exists(currentPath.Value)
                    && File.GetAttributes(currentPath.Value).HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }

                if (!currentPath.TryGetParent(out currentPath))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return true;
        }
    }
}
