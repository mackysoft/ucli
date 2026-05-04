using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Sources;

/// <summary> Validates source definition paths before reading files from SkillDefinitions. </summary>
internal static class SkillSourcePathBoundary
{
    /// <summary> Resolves a source directory and verifies that it remains under the root directory. </summary>
    /// <param name="rootDirectory"> The source root directory. </param>
    /// <param name="directoryPath"> The source directory path. </param>
    /// <returns> The resolved directory path or source-invalid failure. </returns>
    public static SkillOperationResult<string> ResolveDirectoryUnderRoot (
        string rootDirectory,
        string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return ResolveUnderRoot(rootDirectory, directoryPath, resolveFinalFile: false);
    }

    /// <summary> Resolves a source file and verifies that it remains under the root directory. </summary>
    /// <param name="rootDirectory"> The source root directory. </param>
    /// <param name="filePath"> The source file path. </param>
    /// <returns> The resolved file path or source-invalid failure. </returns>
    public static SkillOperationResult<string> ResolveFileUnderRoot (
        string rootDirectory,
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return ResolveUnderRoot(rootDirectory, filePath, resolveFinalFile: true);
    }

    private static SkillOperationResult<string> ResolveUnderRoot (
        string rootDirectory,
        string targetPath,
        bool resolveFinalFile)
    {
        var rootFullPath = ResolveExistingPathSegments(Path.GetFullPath(rootDirectory), resolveFinalFile: false);
        var targetFullPath = ResolveExistingPathSegments(Path.GetFullPath(targetPath), resolveFinalFile);

        if (!IsUnderOrEqual(rootFullPath, targetFullPath))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"SkillDefinitions path must stay under root '{rootFullPath}': {targetFullPath}");
        }

        return SkillOperationResult<string>.Success(targetFullPath);
    }

    private static string ResolveExistingPathSegments (
        string path,
        bool resolveFinalFile)
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

        for (var i = 0; i < segments.Length; i++)
        {
            currentPath = Path.Combine(currentPath, segments[i]);
            var isFinalSegment = i == segments.Length - 1;
            if (Directory.Exists(currentPath))
            {
                var directory = new DirectoryInfo(currentPath);
                var resolvedDirectory = directory.ResolveLinkTarget(returnFinalTarget: true);
                if (resolvedDirectory is not null)
                {
                    currentPath = resolvedDirectory.FullName;
                }

                continue;
            }

            if (resolveFinalFile && isFinalSegment && File.Exists(currentPath))
            {
                var file = new FileInfo(currentPath);
                var resolvedFile = file.ResolveLinkTarget(returnFinalTarget: true);
                if (resolvedFile is not null)
                {
                    currentPath = resolvedFile.FullName;
                }
            }
        }

        return Path.GetFullPath(currentPath);
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

    private static string EnsureTrailingDirectorySeparator (string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
