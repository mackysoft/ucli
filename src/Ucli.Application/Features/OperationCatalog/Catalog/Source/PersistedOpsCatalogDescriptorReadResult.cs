namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents the result of reading one persisted lightweight ops catalog. </summary>
internal sealed record PersistedOpsCatalogDescriptorReadResult (
    OpsCatalogDescriptorSnapshot? Snapshot,
    IndexFreshness? Freshness,
    PersistedOpsCatalogReadFailure? ReadFailure)
{
    /// <summary> Gets a value indicating whether reading succeeded. </summary>
    public bool IsSuccess => Snapshot is not null
        && Freshness.HasValue
        && ReadFailure is null;

    /// <summary> Creates a successful persisted descriptor-catalog read result. </summary>
    public static PersistedOpsCatalogDescriptorReadResult Success (
        OpsCatalogDescriptorSnapshot snapshot,
        IndexFreshness freshness)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new PersistedOpsCatalogDescriptorReadResult(snapshot, freshness, null);
    }

    /// <summary> Creates a failed persisted descriptor-catalog read result. </summary>
    public static PersistedOpsCatalogDescriptorReadResult Failure (PersistedOpsCatalogReadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new PersistedOpsCatalogDescriptorReadResult(null, null, failure);
    }
}
