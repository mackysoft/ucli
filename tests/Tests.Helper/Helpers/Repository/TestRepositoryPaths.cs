namespace MackySoft.Tests;

internal static class TestRepositoryPaths
{
    private static readonly Lazy<string> LazyRepositoryRoot = new(FindRepositoryRoot);

    internal static string RepositoryRoot => LazyRepositoryRoot.Value;

    internal static string GetFullPath (params string[] repositoryRelativeSegments)
    {
        ArgumentNullException.ThrowIfNull(repositoryRelativeSegments);

        return Path.GetFullPath(Path.Combine([RepositoryRoot, .. repositoryRelativeSegments]));
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

}
