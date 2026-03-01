namespace MackySoft.Ucli.Daemon;

/// <summary> Implements filesystem operations for daemon session persistence files. </summary>
internal sealed class DaemonSessionFileAccess : IDaemonSessionFileAccess
{
    /// <summary> Reads daemon session JSON text for one path, or returns <see langword="null" /> when file does not exist. </summary>
    /// <param name="sessionPath"> The session JSON file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The JSON text when file exists; otherwise <see langword="null" />. </returns>
    public async ValueTask<string?> ReadOrNull (
        string sessionPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(sessionPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(sessionPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Writes daemon session JSON text atomically to the target path. </summary>
    /// <param name="sessionPath"> The session JSON file path. </param>
    /// <param name="json"> The daemon session JSON text. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when write operation finishes. </returns>
    public async ValueTask WriteAtomically (
        string sessionPath,
        string json,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directoryPath = Path.GetDirectoryName(sessionPath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {sessionPath}");
        var temporaryPath = sessionPath + $".tmp.{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(directoryPath);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, sessionPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary> Deletes daemon session JSON file when it exists. </summary>
    /// <param name="sessionPath"> The session JSON file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when deletion operation finishes. </returns>
    public ValueTask DeleteIfExists (
        string sessionPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(sessionPath))
        {
            File.Delete(sessionPath);
        }

        return ValueTask.CompletedTask;
    }
}