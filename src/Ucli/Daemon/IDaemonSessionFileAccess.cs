namespace MackySoft.Ucli.Daemon;

/// <summary> Provides filesystem access operations for daemon session persistence files. </summary>
internal interface IDaemonSessionFileAccess
{
    /// <summary> Reads daemon session JSON text for one path, or returns <see langword="null" /> when file does not exist. </summary>
    /// <param name="sessionPath"> The session JSON file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The JSON text when file exists; otherwise <see langword="null" />. </returns>
    ValueTask<string?> ReadOrNull (
        string sessionPath,
        CancellationToken cancellationToken = default);

    /// <summary> Writes daemon session JSON text atomically to the target path. </summary>
    /// <param name="sessionPath"> The session JSON file path. </param>
    /// <param name="json"> The daemon session JSON text. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when write operation finishes. </returns>
    ValueTask WriteAtomically (
        string sessionPath,
        string json,
        CancellationToken cancellationToken = default);

    /// <summary> Deletes daemon session JSON file when it exists. </summary>
    /// <param name="sessionPath"> The session JSON file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when deletion operation finishes. </returns>
    ValueTask Delete (
        string sessionPath,
        CancellationToken cancellationToken = default);
}