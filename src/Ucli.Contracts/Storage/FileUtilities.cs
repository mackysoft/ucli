namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Provides shared utility operations for filesystem files. </summary>
public static class FileUtilities
{
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
        Directory.CreateDirectory(directoryPath);
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
}
