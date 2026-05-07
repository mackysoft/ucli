namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads persisted ops-catalog data and observes its freshness for application policy. </summary>
internal sealed class PersistedOpsCatalogReader : IPersistedOpsCatalogReader
{
    private readonly IReadIndexArtifactReader artifactReader;

    private readonly IReadIndexFreshnessEvaluator freshnessEvaluator;

    /// <summary> Initializes a new instance of the <see cref="PersistedOpsCatalogReader" /> class. </summary>
    /// <param name="artifactReader"> The persisted read-index artifact reader dependency. </param>
    /// <param name="freshnessEvaluator"> The read-index freshness evaluator dependency. </param>
    public PersistedOpsCatalogReader (
        IReadIndexArtifactReader artifactReader,
        IReadIndexFreshnessEvaluator freshnessEvaluator)
    {
        this.artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        this.freshnessEvaluator = freshnessEvaluator ?? throw new ArgumentNullException(nameof(freshnessEvaluator));
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsCatalogReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var opsCatalogResult = await artifactReader.ReadOpsCatalog(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!opsCatalogResult.IsSuccess)
        {
            return PersistedOpsCatalogReadResult.Failure(
                opsCatalogResult.Error!.Code,
                opsCatalogResult.Error.Message);
        }

        var opsCatalog = opsCatalogResult.Value!;
        var freshnessResult = await freshnessEvaluator.Observe(
                unityProject,
                IndexFreshnessTarget.OpsCatalog,
                opsCatalog.SourceInputsHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return PersistedOpsCatalogReadResult.Failure(
                freshnessResult.Error!.Code,
                freshnessResult.Error.Message);
        }

        return PersistedOpsCatalogReadResult.Success(
            opsCatalog.Entries!,
            opsCatalog.GeneratedAtUtc,
            freshnessResult.Freshness);
    }
}
