using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Validates filesystem paths against an allowed root boundary. </summary>
internal static class SkillPathBoundary
{
    /// <summary> Resolves a target path and verifies that it remains under the allowed root. </summary>
    /// <param name="rootPath"> The allowed root path. </param>
    /// <param name="targetPath"> The target path. </param>
    /// <returns> The canonical target path or path-safety failure. </returns>
    public static SkillOperationResult<string> ResolveUnderRoot (
        string rootPath,
        string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var rootFullPath = ResolveExistingPathSegments(Path.GetFullPath(rootPath));
        var targetFullPath = ResolveExistingPathSegments(Path.GetFullPath(targetPath));

        if (!IsUnderOrEqual(rootFullPath, targetFullPath))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Path must stay under root '{rootFullPath}': {targetFullPath}");
        }

        return SkillOperationResult<string>.Success(targetFullPath);
    }

    /// <summary> Verifies that a package file path remains under the target directory. </summary>
    /// <param name="targetDirectory"> The target directory. </param>
    /// <param name="relativePath"> The package-relative path. </param>
    /// <returns> The canonical file path or path-safety failure. </returns>
    public static SkillOperationResult<string> ResolvePackageFilePath (
        string targetDirectory,
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.Split('/').Any(static segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package file path is unsafe: {relativePath}");
        }

        return ResolveUnderRoot(targetDirectory, Path.Combine(targetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool IsUnderOrEqual (
        string rootPath,
        string targetPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalizedRoot = EnsureTrailingDirectorySeparator(rootPath);
        var normalizedTarget = EnsureTrailingDirectorySeparator(targetPath);
        return string.Equals(normalizedRoot, normalizedTarget, comparison)
            || normalizedTarget.StartsWith(normalizedRoot, comparison);
    }

    private static string ResolveExistingPathSegments (string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return path;
        }

        var currentPath = root;
        var relativePath = path[root.Length..];
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!Directory.Exists(currentPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(currentPath);
            var resolved = directory.ResolveLinkTarget(returnFinalTarget: true);
            if (resolved is not null)
            {
                currentPath = resolved.FullName;
            }
        }

        return Path.GetFullPath(currentPath);
    }

    private static string EnsureTrailingDirectorySeparator (string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
