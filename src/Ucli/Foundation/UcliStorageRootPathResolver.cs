namespace MackySoft.Ucli.Foundation;

/// <summary> Resolves the storage root path used by shared <c>.ucli</c> files. </summary>
internal static class UcliStorageRootPathResolver
{
    /// <summary> Resolves the storage root from a starting directory path. </summary>
    /// <param name="startPath"> The starting directory path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns>
    /// <para> The repository root path when a <c>.git</c> marker is found on the current or parent directories. </para>
    /// <para> Otherwise, the normalized absolute <paramref name="startPath" />. </para>
    /// </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="startPath" /> contains invalid path characters. </exception>
    /// <exception cref="NotSupportedException"> Thrown when <paramref name="startPath" /> uses an unsupported path format. </exception>
    /// <exception cref="PathTooLongException"> Thrown when <paramref name="startPath" /> exceeds platform path limits. </exception>
    public static string Resolve (string startPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

        var normalizedStartPath = Path.GetFullPath(startPath);
        var repositoryRoot = RepositoryRootPathResolver.TryResolve(normalizedStartPath);
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return repositoryRoot;
        }

        // NOTE:
        // Local and CI environments may not have a Git repository.
        // Use the starting path as a deterministic fallback storage root.
        return normalizedStartPath;
    }
}