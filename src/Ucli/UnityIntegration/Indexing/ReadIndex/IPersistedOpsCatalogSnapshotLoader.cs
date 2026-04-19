using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Loads one persisted ops-catalog snapshot and observes its freshness. </summary>
internal interface IPersistedOpsCatalogSnapshotLoader
{
    /// <summary> Loads the persisted ops-catalog snapshot for one resolved Unity project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The snapshot load result. </returns>
    ValueTask<PersistedOpsCatalogSnapshotLoadResult> Load (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}