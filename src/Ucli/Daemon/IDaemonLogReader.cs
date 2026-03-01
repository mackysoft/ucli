namespace MackySoft.Ucli.Daemon;

/// <summary> Reads daemon log text segments from local storage. </summary>
internal interface IDaemonLogReader
{
    /// <summary> Reads the tail segment of daemon log file for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="maxBytes"> The maximum number of bytes to read from the end of daemon log file. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon log read result. </returns>
    ValueTask<DaemonLogReadResult> ReadTail (
        string storageRoot,
        string projectFingerprint,
        int maxBytes = DaemonLogReader.DefaultMaxBytes,
        CancellationToken cancellationToken = default);
}