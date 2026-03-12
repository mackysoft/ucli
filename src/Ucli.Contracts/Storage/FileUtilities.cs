namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Provides shared utility operations for filesystem files. </summary>
public static class FileUtilities
{
    private const string LocalDirectoryIgnoreEntry = UcliStoragePathNames.LocalDirectoryName + "/";

    /// <summary> Reads one file as text, or returns <see langword="null" /> when file does not exist. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The text when file exists; otherwise <see langword="null" />. </returns>
    public static async ValueTask<string?> ReadAllTextOrNull (
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary> Writes text atomically to the target file path. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="contents"> The text contents. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when the write operation finishes. </returns>
    public static async ValueTask WriteAllTextAtomically (
        string path,
        string contents,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        if (contents == null)
        {
            throw new ArgumentNullException(nameof(contents));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var directoryPath = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        EnsureStorageDirectoryExists(directoryPath);
        var temporaryPath = path + $".tmp.{Guid.NewGuid():N}";

        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents, cancellationToken).ConfigureAwait(false);
            ReplaceFile(temporaryPath, path);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
        }
    }

    /// <summary> Ensures one storage directory exists and bootstraps shared <c>.ucli/local</c> metadata when applicable. </summary>
    /// <param name="directoryPath"> The target directory path. </param>
    public static void EnsureStorageDirectoryExists (string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("directoryPath must not be empty.", nameof(directoryPath));
        }

        var normalizedDirectoryPath = Path.GetFullPath(directoryPath);
        if (TryResolveUcliLocalRoot(normalizedDirectoryPath, out var ucliDirectoryPath, out var localDirectoryPath))
        {
            Directory.CreateDirectory(ucliDirectoryPath!);
            EnsureLocalGitIgnoreExists(ucliDirectoryPath!);
            Directory.CreateDirectory(localDirectoryPath!);
        }

        Directory.CreateDirectory(normalizedDirectoryPath);
    }

    /// <summary> Deletes one file and treats a missing file as a valid no-op state. </summary>
    /// <param name="path"> The target file path. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is invalid. </exception>
    public static void DeleteIfExists (string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ReplaceFile (
        string temporaryPath,
        string path)
    {
        try
        {
            File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
        }
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

    private static bool TryResolveUcliLocalRoot (
        string directoryPath,
        out string? ucliDirectoryPath,
        out string? localDirectoryPath)
    {
        var comparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var currentDirectory = new DirectoryInfo(directoryPath);
        while (currentDirectory != null)
        {
            var parentDirectory = currentDirectory.Parent;
            if (string.Equals(currentDirectory.Name, UcliStoragePathNames.LocalDirectoryName, comparison)
                && parentDirectory != null
                && string.Equals(parentDirectory.Name, UcliStoragePathNames.UcliDirectoryName, comparison))
            {
                ucliDirectoryPath = parentDirectory.FullName;
                localDirectoryPath = currentDirectory.FullName;
                return true;
            }

            currentDirectory = parentDirectory;
        }

        ucliDirectoryPath = null;
        localDirectoryPath = null;
        return false;
    }
}