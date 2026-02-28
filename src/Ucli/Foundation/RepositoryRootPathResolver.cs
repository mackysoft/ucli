namespace MackySoft.Ucli.Foundation;

/// <summary> Resolves a repository root path by searching parent directories for a <c>.git</c> marker. </summary>
internal static class RepositoryRootPathResolver
{
    private const string GitMarkerName = ".git";

    /// <summary> Resolves the repository root path from a starting directory path. </summary>
    /// <param name="startPath"> The starting directory path. </param>
    /// <returns>
    /// <para> The repository root path when a <c>.git</c> marker is found. </para>
    /// <para> Otherwise, <see langword="null" />. </para>
    /// </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> contains invalid path characters. </exception>
    /// <exception cref="NotSupportedException"> Thrown when <paramref name="startPath" /> uses an unsupported path format. </exception>
    /// <exception cref="PathTooLongException"> Thrown when <paramref name="startPath" /> exceeds platform path limits. </exception>
    public static string? TryResolve (string startPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

        var directoryPath = Path.GetFullPath(startPath);
        if (!Directory.Exists(directoryPath))
        {
            return null;
        }

        while (true)
        {
            var markerPath = Path.Combine(directoryPath, GitMarkerName);
            if (Directory.Exists(markerPath) || File.Exists(markerPath))
            {
                return directoryPath;
            }

            var parentDirectory = Directory.GetParent(directoryPath);
            if (parentDirectory is null)
            {
                return null;
            }

            directoryPath = parentDirectory.FullName;
        }
    }
}