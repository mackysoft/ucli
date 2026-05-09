namespace MackySoft.Ucli.Skills.Packaging;

/// <summary> Provides file operations needed while installing or exporting materialized SKILL files. </summary>
internal static class SkillPackageFileWriter
{
    /// <summary> Writes text atomically to the target file path. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="contents"> The text contents. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when the write operation finishes. </returns>
    public static async ValueTask WriteAllTextAtomicallyAsync (
        string path,
        string contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);
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

    private static void DeleteIfExists (string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
