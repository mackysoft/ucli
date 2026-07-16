namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Reads and atomically publishes the current immutable read-index generation identifier. </summary>
internal interface IReadIndexGenerationPointerStore
{
    /// <summary> Reads the current generation identifier. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The current non-empty identifier, or <see langword="null" /> when no generation has committed. </returns>
    /// <exception cref="InvalidDataException"> Thrown when the persisted pointer is not one canonical non-empty GUID. </exception>
    ValueTask<Guid?> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken);

    /// <summary> Atomically replaces the current generation identifier. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint. </param>
    /// <param name="generationId"> The committed non-empty generation identifier. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> A task that completes after the pointer replacement is durable to the filesystem API boundary. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="generationId" /> is empty. </exception>
    ValueTask PublishAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId,
        CancellationToken cancellationToken);
}
