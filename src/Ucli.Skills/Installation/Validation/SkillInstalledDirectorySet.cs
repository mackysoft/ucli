using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation.Validation;

/// <summary> Builds and verifies installed package directory entries. </summary>
internal static class SkillInstalledDirectorySet
{
    /// <summary> Builds the directory set required by package-relative file paths. </summary>
    /// <param name="relativeFilePaths"> The package-relative file paths. </param>
    /// <returns> Directory paths that may exist below the package root. </returns>
    public static HashSet<string> BuildParentDirectories (IEnumerable<string> relativeFilePaths)
    {
        ArgumentNullException.ThrowIfNull(relativeFilePaths);

        var directoryPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relativeFilePath in relativeFilePaths)
        {
            AddParentDirectories(directoryPaths, relativeFilePath);
        }

        return directoryPaths;
    }

    /// <summary> Adds every parent directory of one package-relative file path. </summary>
    /// <param name="directoryPaths"> The mutable directory set. </param>
    /// <param name="relativeFilePath"> The package-relative file path. </param>
    public static void AddParentDirectories (
        HashSet<string> directoryPaths,
        string relativeFilePath)
    {
        ArgumentNullException.ThrowIfNull(directoryPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeFilePath);

        var normalizedPath = relativeFilePath.Replace('\\', '/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        while (lastSeparatorIndex > 0)
        {
            var directoryPath = normalizedPath[..lastSeparatorIndex];
            directoryPaths.Add(directoryPath);
            lastSeparatorIndex = directoryPath.LastIndexOf('/');
        }
    }

    /// <summary> Checks that every installed directory entry is explicitly allowed. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="allowedDirectoryPaths"> The package-relative directories that may exist. </param>
    /// <returns> <see langword="true" /> when the directory set is exact; otherwise <see langword="false" />. </returns>
    public static SkillOperationResult<bool> ContainsOnlyAllowedDirectories (
        string skillDirectory,
        IReadOnlySet<string> allowedDirectoryPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(allowedDirectoryPaths);

        foreach (var directoryPath in Directory.EnumerateDirectories(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var directoryPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, directoryPath);
            if (!directoryPathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(directoryPathResult.Failure!.Code, directoryPathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, Path.GetFullPath(directoryPath)).Replace(Path.DirectorySeparatorChar, '/');
            if (!allowedDirectoryPaths.Contains(relativePath))
            {
                return SkillOperationResult<bool>.Success(false);
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }
}
