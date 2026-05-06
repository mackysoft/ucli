namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Provides read-index input fingerprints without exposing filesystem traversal details to application policy. </summary>
internal interface IReadIndexInputFingerprintProvider
{
    /// <summary> Tries to compute one core input fingerprint snapshot. </summary>
    ValueTask<ReadIndexCoreInputHashSnapshot?> TryComputeCore (
        string projectRootPath,
        CancellationToken cancellationToken = default);

    /// <summary> Tries to compute one full input fingerprint snapshot. </summary>
    ValueTask<ReadIndexInputHashSnapshot?> TryCompute (
        string projectRootPath,
        CancellationToken cancellationToken = default);
}
