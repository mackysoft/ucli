using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Loads persisted ops-catalog data and computes observed freshness without applying caller-specific policy. </summary>
internal sealed class PersistedOpsCatalogSnapshotLoader : IPersistedOpsCatalogSnapshotLoader
{
    private readonly IIndexCatalogReader indexCatalogReader;

    private readonly IIndexFreshnessEvaluator indexFreshnessEvaluator;

    /// <summary> Initializes a new instance of the <see cref="PersistedOpsCatalogSnapshotLoader" /> class. </summary>
    /// <param name="indexCatalogReader"> The persisted index reader dependency. </param>
    /// <param name="indexFreshnessEvaluator"> The read-index freshness evaluator dependency. </param>
    public PersistedOpsCatalogSnapshotLoader (
        IIndexCatalogReader indexCatalogReader,
        IIndexFreshnessEvaluator indexFreshnessEvaluator)
    {
        this.indexCatalogReader = indexCatalogReader ?? throw new ArgumentNullException(nameof(indexCatalogReader));
        this.indexFreshnessEvaluator = indexFreshnessEvaluator ?? throw new ArgumentNullException(nameof(indexFreshnessEvaluator));
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsCatalogSnapshotLoadResult> Load (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var opsCatalogResult = await indexCatalogReader.ReadOpsCatalog(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!opsCatalogResult.IsSuccess)
        {
            return PersistedOpsCatalogSnapshotLoadResult.Failure(opsCatalogResult.Error!);
        }

        var opsCatalog = opsCatalogResult.Value!;
        var freshnessResult = await indexFreshnessEvaluator.Evaluate(
                unityProject.UnityProjectRoot,
                IndexFreshnessTarget.OpsCatalog,
                opsCatalog.SourceInputsHash,
                ReadIndexMode.AllowStale,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return PersistedOpsCatalogSnapshotLoadResult.Failure(freshnessResult.Error!);
        }

        return PersistedOpsCatalogSnapshotLoadResult.Success(
            new PersistedOpsCatalogSnapshot(
                Entries: opsCatalog.Entries!,
                GeneratedAtUtc: opsCatalog.GeneratedAtUtc,
                Freshness: freshnessResult.Freshness));
    }
}