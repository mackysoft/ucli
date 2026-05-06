namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class ArchitectureTestRepository
{
    private static readonly string[] GeneratedDirectoryNames =
    [
        "bin",
        "obj",
        "Library",
        "Temp",
    ];

    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static string ToFullPath (string repositoryRelativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(RepositoryRoot, repositoryRelativePath));
        if (!IsRepositoryPath(fullPath))
        {
            throw new InvalidOperationException($"Path escapes repository root: {repositoryRelativePath}");
        }

        return fullPath;
    }

    internal static string ToRegularFileFullPath (string repositoryRelativePath)
    {
        var fullPath = ToFullPath(repositoryRelativePath);
        if (IsReparsePoint(fullPath) || HasReparsePointParent(fullPath))
        {
            throw new InvalidOperationException($"Repository file must not be a reparse point: {repositoryRelativePath}");
        }

        return fullPath;
    }

    internal static IEnumerable<string> EnumerateCSharpSourceFiles (string repositoryRelativeDirectory)
    {
        return EnumerateRepositoryFiles(ToFullPath(repositoryRelativeDirectory), "*.cs")
            .Where(static sourceFile =>
            {
                var relativePath = NormalizeRepositoryRelativePath(sourceFile);
                return !IsGeneratedPath(relativePath) && !IsReparsePoint(sourceFile);
            });
    }

    internal static IEnumerable<string> EnumerateProductionProjectFiles ()
    {
        return EnumerateRepositoryFiles(ToFullPath("src"), "*.csproj")
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !IsUnityGeneratedProjectFile(relativePath));
    }

    internal static IEnumerable<string> EnumerateTestProjectFiles ()
    {
        return EnumerateRepositoryFiles(ToFullPath("tests"), "*.csproj")
            .Select(NormalizeRepositoryRelativePath);
    }

    internal static IEnumerable<string> EnumerateMsBuildImportFiles ()
    {
        return EnumerateRepositoryFiles(RepositoryRoot, "*.*")
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => relativePath.EndsWith(".props", StringComparison.Ordinal)
                                          || relativePath.EndsWith(".targets", StringComparison.Ordinal))
            .Where(static relativePath => !IsGeneratedPath(relativePath));
    }

    internal static IEnumerable<string> EnumerateAssemblyInfoFiles (IEnumerable<string> repositoryRelativeRoots)
    {
        return repositoryRelativeRoots
            .Select(ToFullPath)
            .SelectMany(static root => EnumerateRepositoryFiles(root, "AssemblyInfo.cs"))
            .Select(NormalizeRepositoryRelativePath)
            .Where(static relativePath => !IsGeneratedPath(relativePath));
    }

    internal static string NormalizeRepositoryRelativePath (string fullPath)
    {
        return Path.GetRelativePath(RepositoryRoot, fullPath).Replace('\\', '/');
    }

    private static bool IsUnityGeneratedProjectFile (string relativePath)
    {
        return relativePath.StartsWith("src/Ucli.Unity/", StringComparison.Ordinal)
            && relativePath.EndsWith(".csproj", StringComparison.Ordinal);
    }

    private static bool IsGeneratedPath (string relativePath)
    {
        foreach (var directoryName in GeneratedDirectoryNames)
        {
            if (relativePath.Equals(directoryName, StringComparison.Ordinal)
                || relativePath.StartsWith($"{directoryName}/", StringComparison.Ordinal)
                || relativePath.Contains($"/{directoryName}/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateRepositoryFiles (string rootDirectory, string searchPattern)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);
        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            foreach (var file in Directory.EnumerateFiles(currentDirectory, searchPattern))
            {
                if (!IsReparsePoint(file))
                {
                    yield return file;
                }
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory))
            {
                if (!IsGeneratedDirectory(childDirectory) && !IsReparsePoint(childDirectory))
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }
    }

    private static bool IsGeneratedDirectory (string directory)
    {
        var directoryName = Path.GetFileName(directory);
        return GeneratedDirectoryNames.Contains(directoryName, StringComparer.Ordinal);
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

    private static bool IsRepositoryPath (string fullPath)
    {
        var rootWithSeparator = RepositoryRoot.EndsWith(Path.DirectorySeparatorChar)
            ? RepositoryRoot
            : $"{RepositoryRoot}{Path.DirectorySeparatorChar}";
        return fullPath.Equals(RepositoryRoot, StringComparison.Ordinal)
            || fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    internal static bool IsReparsePoint (string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static string FindRepositoryRoot ()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory != null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Ucli.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
