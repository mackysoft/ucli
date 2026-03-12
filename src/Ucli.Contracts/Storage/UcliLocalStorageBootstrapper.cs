namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Applies shared bootstrap rules for writes under <c>.ucli/local</c>. </summary>
public static class UcliLocalStorageBootstrapper
{
    /// <summary> Gets the git-ignore entry used to exclude local runtime storage from version control. </summary>
    public const string LocalDirectoryIgnoreEntry = UcliStoragePathNames.LocalDirectoryName + "/";

    /// <summary> Ensures shared local-storage metadata exists when the target directory is under <c>.ucli/local</c>. </summary>
    /// <param name="directoryPath"> The target directory path. </param>
    public static void EnsureInitialized (string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("directoryPath must not be empty.", nameof(directoryPath));
        }

        if (!UcliStoragePathResolver.TryResolveLocalStorageRootDirectories(
                directoryPath,
                out var ucliDirectoryPath,
                out var localDirectoryPath))
        {
            return;
        }

        Directory.CreateDirectory(ucliDirectoryPath!);
        EnsureLocalGitIgnoreExists(ucliDirectoryPath!);
        Directory.CreateDirectory(localDirectoryPath!);
    }

    private static void EnsureLocalGitIgnoreExists (string ucliDirectoryPath)
    {
        var gitIgnorePath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.GitIgnoreFileName);
        if (File.Exists(gitIgnorePath))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(gitIgnorePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(LocalDirectoryIgnoreEntry);
            writer.Write(Environment.NewLine);
        }
        catch (IOException) when (File.Exists(gitIgnorePath))
        {
        }
    }
}