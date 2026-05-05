using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Represents the result of loading one persisted ops-catalog snapshot. </summary>
/// <param name="Snapshot"> The persisted snapshot on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The machine-readable index error on failure; otherwise <see langword="null" />. </param>
internal sealed record PersistedOpsCatalogSnapshotLoadResult (
    PersistedOpsCatalogSnapshot? Snapshot,
    IndexServiceError? Error)
{
    /// <summary> Gets a value indicating whether loading succeeded. </summary>
    public bool IsSuccess => Snapshot is not null && Error is null;

    /// <summary> Creates a successful persisted-snapshot load result. </summary>
    /// <param name="snapshot"> The loaded snapshot. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="snapshot" /> is <see langword="null" />. </exception>
    public static PersistedOpsCatalogSnapshotLoadResult Success (PersistedOpsCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new PersistedOpsCatalogSnapshotLoadResult(snapshot, null);
    }

    /// <summary> Creates a failed persisted-snapshot load result. </summary>
    /// <param name="error"> The machine-readable index error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static PersistedOpsCatalogSnapshotLoadResult Failure (IndexServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new PersistedOpsCatalogSnapshotLoadResult(null, error);
    }
}
