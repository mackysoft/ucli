namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents the result of reading one persisted ops catalog. </summary>
/// <param name="Snapshot"> The persisted catalog snapshot on success; otherwise <see langword="null" />. </param>
/// <param name="Freshness"> The observed persisted freshness on success; otherwise <see langword="null" />. </param>
/// <param name="ReadFailure"> The classified failure on failure; otherwise <see langword="null" />. </param>
internal sealed record PersistedOpsCatalogReadResult (
    OpsCatalogSnapshot? Snapshot,
    IndexFreshness? Freshness,
    PersistedOpsCatalogReadFailure? ReadFailure)
{
    /// <summary> Gets a value indicating whether reading succeeded. </summary>
    public bool IsSuccess => Snapshot is not null
        && Freshness.HasValue
        && ReadFailure is null;

    /// <summary> Creates a successful persisted-catalog read result. </summary>
    /// <param name="snapshot"> The persisted catalog snapshot. </param>
    /// <param name="freshness"> The observed persisted freshness. </param>
    /// <returns> The successful read result. </returns>
    public static PersistedOpsCatalogReadResult Success (
        OpsCatalogSnapshot snapshot,
        IndexFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new PersistedOpsCatalogReadResult(
            Snapshot: snapshot,
            Freshness: freshness,
            ReadFailure: null);
    }

    /// <summary> Creates a failed persisted-catalog read result. </summary>
    /// <param name="failure"> The classified failure. </param>
    /// <returns> The failed read result. </returns>
    public static PersistedOpsCatalogReadResult Failure (PersistedOpsCatalogReadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new PersistedOpsCatalogReadResult(
            Snapshot: null,
            Freshness: null,
            ReadFailure: failure);
    }

}
