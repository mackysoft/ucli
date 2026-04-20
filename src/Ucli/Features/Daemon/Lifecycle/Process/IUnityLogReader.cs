namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Reads Unity batchmode log text segments from local storage. </summary>
internal interface IUnityLogReader
{
    /// <summary> Reads the tail segment of Unity log file for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="maxBytes"> The maximum number of bytes to read from the end of Unity log file. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Unity log read result. </returns>
    ValueTask<UnityLogReadResult> ReadTail (
        string storageRoot,
        string projectFingerprint,
        int maxBytes = UnityLogReader.DefaultMaxBytes,
        CancellationToken cancellationToken = default);
}