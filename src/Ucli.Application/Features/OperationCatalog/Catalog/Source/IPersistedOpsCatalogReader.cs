namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads one persisted ops catalog together with caller-facing freshness metadata. </summary>
internal interface IPersistedOpsCatalogReader
{
    /// <summary> Reads the persisted ops catalog for one resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The persisted ops-catalog read result. </returns>
    ValueTask<PersistedOpsCatalogReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads the persisted lightweight ops catalog for one resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The persisted lightweight ops-catalog read result. </returns>
    ValueTask<PersistedOpsCatalogDescriptorReadResult> ReadDescriptors (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one persisted operation detail artifact referenced by a lightweight ops catalog. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="catalogSnapshot"> The lightweight catalog snapshot. </param>
    /// <param name="catalogEntry"> The matching lightweight catalog entry. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The persisted describe read result. </returns>
    ValueTask<PersistedOpsDescribeReadResult> ReadDescribe (
        ResolvedUnityProjectContext unityProject,
        OpsCatalogDescriptorSnapshot catalogSnapshot,
        IndexOpsCatalogEntryJsonContract catalogEntry,
        CancellationToken cancellationToken = default);
}
