using System.Collections.Concurrent;

namespace MackySoft.Tests;

internal static class TestRepositoryPaths
{
    private static readonly string[] GeneratedDirectoryNames =
    [
        "bin",
        "obj",
        "Library",
        "Temp",
    ];

    private static readonly Lazy<string> LazyRepositoryRoot = new(FindRepositoryRoot);

    private static readonly ConcurrentDictionary<(string RootDirectory, string SearchPattern, bool SkipGeneratedDirectories), Lazy<string[]>> FileCache = [];

    internal static string RepositoryRoot => LazyRepositoryRoot.Value;

    internal static string GetFullPath (params string[] repositoryRelativeSegments)
    {
        ArgumentNullException.ThrowIfNull(repositoryRelativeSegments);

        return Path.GetFullPath(Path.Combine([RepositoryRoot, .. repositoryRelativeSegments]));
    }

    internal static IEnumerable<string> EnumerateRegularFiles (
        string repositoryRelativeDirectory,
        string searchPattern,
        bool skipGeneratedDirectories = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativeDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        var rootDirectory = GetFullPath(repositoryRelativeDirectory);
        if (!IsRepositoryPath(rootDirectory))
        {
            throw new InvalidOperationException($"Path escapes repository root: {repositoryRelativeDirectory}");
        }

        return FileCache.GetOrAdd(
            (rootDirectory, searchPattern, skipGeneratedDirectories),
            static cacheKey => new Lazy<string[]>(
                () => EnumerateRegularFilesCore(
                        cacheKey.RootDirectory,
                        cacheKey.SearchPattern,
                        cacheKey.SkipGeneratedDirectories)
                    .ToArray(),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    internal static IEnumerable<string> EnumerateRegularFilesUnderDirectory (
        string rootDirectory,
        string searchPattern = "*",
        bool skipGeneratedDirectories = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        return EnumerateRegularFilesCore(
            Path.GetFullPath(rootDirectory),
            searchPattern,
            skipGeneratedDirectories);
    }

    internal static string GetRegularFileFullPath (params string[] repositoryRelativeSegments)
    {
        var fullPath = GetFullPath(repositoryRelativeSegments);
        if (IsReparsePoint(fullPath) || HasReparsePointParent(fullPath))
        {
            throw new InvalidOperationException($"Repository file must not be a reparse point: {NormalizeRepositoryRelativePath(fullPath)}");
        }

        return fullPath;
    }

    internal static string GetRegularDirectoryFullPath (params string[] repositoryRelativeSegments)
    {
        var fullPath = GetFullPath(repositoryRelativeSegments);
        if (IsReparsePoint(fullPath) || HasReparsePointParent(fullPath))
        {
            throw new InvalidOperationException($"Repository directory must not be a reparse point: {NormalizeRepositoryRelativePath(fullPath)}");
        }

        return fullPath;
    }

    internal static string NormalizeRepositoryRelativePath (string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        return Path.GetRelativePath(RepositoryRoot, fullPath).Replace('\\', '/');
    }

    private static string FindRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ucli.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test base directory.");
    }

    private static IEnumerable<string> EnumerateRegularFilesCore (
        string rootDirectory,
        string searchPattern,
        bool skipGeneratedDirectories)
    {
        if (IsReparsePoint(rootDirectory))
        {
            throw new InvalidOperationException($"Root directory must not be a reparse point: {rootDirectory}");
        }

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);
        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            foreach (var file in Directory.EnumerateFiles(currentDirectory, searchPattern).Order(StringComparer.Ordinal))
            {
                if (!IsReparsePoint(file))
                {
                    yield return file;
                }
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory).Order(StringComparer.Ordinal))
            {
                if ((!skipGeneratedDirectories || !IsGeneratedDirectory(childDirectory)) && !IsReparsePoint(childDirectory))
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }
    }

    private static bool HasReparsePointParent (string fullPath)
    {
        var currentDirectory = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrWhiteSpace(currentDirectory) && IsRepositoryPath(currentDirectory))
        {
            if (!currentDirectory.Equals(RepositoryRoot, StringComparison.Ordinal)
                && IsReparsePoint(currentDirectory))
            {
                return true;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        return false;
    }

    private static bool IsGeneratedDirectory (string directory)
    {
        var directoryName = Path.GetFileName(directory);
        return GeneratedDirectoryNames.Contains(directoryName, StringComparer.Ordinal);
    }

    private static bool IsRepositoryPath (string fullPath)
    {
        var relativePath = Path.GetRelativePath(RepositoryRoot, fullPath);
        return relativePath == "."
            || (!relativePath.StartsWith("..", StringComparison.Ordinal)
                && !Path.IsPathRooted(relativePath));
    }

    internal static bool IsReparsePoint (string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
