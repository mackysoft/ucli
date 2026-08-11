namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Resolves a candidate path through every existing symbolic-link ancestor and enforces one root boundary. </summary>
internal static class ProgramReferencePathResolver
{
    /// <summary> Tries to resolve one candidate path inside the physical reference root. </summary>
    public static bool TryResolveWithinRoot (string rootPath, string candidatePath, out string resolvedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);

        var physicalRoot = ResolveExistingLinks(Path.GetFullPath(rootPath));
        resolvedPath = ResolveExistingLinks(Path.GetFullPath(candidatePath));
        return IsWithinRoot(physicalRoot, resolvedPath);
    }

    private static string ResolveExistingLinks (string path)
    {
        var root = Path.GetPathRoot(path) ?? throw new InvalidOperationException($"Path root cannot be resolved: {path}.");
        var current = root;
        var relativePath = path[root.Length..];
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!TryResolveLinkTarget(current, out var target))
            {
                continue;
            }

            current = ResolveExistingLinks(target);
        }

        return current;
    }

    private static bool TryResolveLinkTarget (string path, out string target)
    {
        target = string.Empty;
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            var resolvedTarget = info.ResolveLinkTarget(returnFinalTarget: true);
            if (resolvedTarget is null)
            {
                return false;
            }

            target = resolvedTarget.FullName;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsWithinRoot (string root, string candidate)
    {
        var normalizedRoot = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return string.Equals(root, candidate, StringComparison.Ordinal)
            || candidate.StartsWith(normalizedRoot, StringComparison.Ordinal);
    }
}
