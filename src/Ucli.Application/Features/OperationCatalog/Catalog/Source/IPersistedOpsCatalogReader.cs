namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads one persisted ops catalog together with caller-facing freshness metadata. </summary>
internal interface IPersistedOpsCatalogReader
{
    /// <summary> Reads the persisted ops catalog for one resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The persisted ops-catalog read result. </returns>
    ValueTask<PersistedOpsCatalogReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
