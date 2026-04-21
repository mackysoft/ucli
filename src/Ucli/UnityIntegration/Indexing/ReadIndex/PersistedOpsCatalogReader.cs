using MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Adapts the persisted read-index loader to the operation-catalog source seam. </summary>
internal sealed class PersistedOpsCatalogReader : IPersistedOpsCatalogReader
{
    private readonly IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader;

    /// <summary> Initializes a new instance of the <see cref="PersistedOpsCatalogReader" /> class. </summary>
    /// <param name="persistedOpsCatalogSnapshotLoader"> The persisted snapshot loader dependency. </param>
    public PersistedOpsCatalogReader (IPersistedOpsCatalogSnapshotLoader persistedOpsCatalogSnapshotLoader)
    {
        this.persistedOpsCatalogSnapshotLoader = persistedOpsCatalogSnapshotLoader ?? throw new ArgumentNullException(nameof(persistedOpsCatalogSnapshotLoader));
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsCatalogReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var snapshotResult = await persistedOpsCatalogSnapshotLoader.Load(unityProject, cancellationToken).ConfigureAwait(false);
        if (!snapshotResult.IsSuccess)
        {
            return PersistedOpsCatalogReadResult.Failure(
                snapshotResult.Error!.Code,
                snapshotResult.Error.Message);
        }

        return PersistedOpsCatalogReadResult.Success(
            snapshotResult.Snapshot!.Entries,
            snapshotResult.Snapshot.GeneratedAtUtc,
            snapshotResult.Snapshot.Freshness);
    }
}