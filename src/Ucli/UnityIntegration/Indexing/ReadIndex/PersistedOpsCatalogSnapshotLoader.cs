using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Loads persisted ops-catalog data and computes observed freshness without applying caller-specific policy. </summary>
internal sealed class PersistedOpsCatalogSnapshotLoader : IPersistedOpsCatalogSnapshotLoader
{
    private readonly IReadIndexArtifactReader artifactReader;

    private readonly IReadIndexFreshnessEvaluator freshnessEvaluator;

    /// <summary> Initializes a new instance of the <see cref="PersistedOpsCatalogSnapshotLoader" /> class. </summary>
    /// <param name="artifactReader"> The persisted read-index artifact reader dependency. </param>
    /// <param name="freshnessEvaluator"> The read-index freshness evaluator dependency. </param>
    public PersistedOpsCatalogSnapshotLoader (
        IReadIndexArtifactReader artifactReader,
        IReadIndexFreshnessEvaluator freshnessEvaluator)
    {
        this.artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        this.freshnessEvaluator = freshnessEvaluator ?? throw new ArgumentNullException(nameof(freshnessEvaluator));
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsCatalogSnapshotLoadResult> Load (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var opsCatalogResult = await artifactReader.ReadOpsCatalog(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!opsCatalogResult.IsSuccess)
        {
            return PersistedOpsCatalogSnapshotLoadResult.Failure(opsCatalogResult.Error!);
        }

        var opsCatalog = opsCatalogResult.Value!;
        var freshnessResult = await freshnessEvaluator.Evaluate(
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
