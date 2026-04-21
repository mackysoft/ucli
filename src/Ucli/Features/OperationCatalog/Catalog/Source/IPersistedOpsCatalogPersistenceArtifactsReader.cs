namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads persisted read-index artifacts used when refreshing and persisting the ops catalog. </summary>
internal interface IPersistedOpsCatalogPersistenceArtifactsReader
{
    /// <summary> Reads persisted inputs-manifest data and lookup-artifact presence for one Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The persisted artifact state used to persist refreshed ops catalogs. </returns>
    ValueTask<PersistedOpsCatalogPersistenceArtifacts> Read (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}