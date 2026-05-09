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
    public async ValueTask<PersistedOpsCatalogReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var opsCatalogResult = await artifactReader.ReadOpsCatalogAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!opsCatalogResult.IsSuccess)
        {
            return PersistedOpsCatalogReadResult.Failure(CreateArtifactFailure(opsCatalogResult.Error!));
        }

        var opsCatalog = opsCatalogResult.Value!;
        if (!OpsCatalogSnapshot.TryCreate(
                opsCatalog.GeneratedAtUtc,
                opsCatalog.Entries,
                "entries",
                out var snapshot,
                out var validationError))
        {
            return PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Malformed,
                    ReadIndexErrorCodes.ReadIndexFormatInvalid,
                    $"Index contract file 'ops.catalog.json' is malformed. {validationError}"));
        }

        var freshnessResult = await freshnessEvaluator.ObserveAsync(
                unityProject,
                IndexFreshnessTarget.OpsCatalog,
                opsCatalog.SourceInputsHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.FreshnessUnavailable,
                    freshnessResult.Error!.Code,
                    freshnessResult.Error.Message));
        }

        return PersistedOpsCatalogReadResult.Success(
            snapshot!,
            freshnessResult.Freshness);
    }

    private static PersistedOpsCatalogReadFailure CreateArtifactFailure (IndexServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new PersistedOpsCatalogReadFailure(
            ClassifyArtifactFailure(error.Code),
            error.Code,
            error.Message);
    }

    private static PersistedOpsCatalogReadFailureKind ClassifyArtifactFailure (UcliErrorCode errorCode)
    {
        if (errorCode == UcliCoreErrorCodes.InvalidArgument)
        {
            return PersistedOpsCatalogReadFailureKind.InvalidArgument;
        }

        if (errorCode == ReadIndexErrorCodes.ReadIndexFormatInvalid)
        {
            return PersistedOpsCatalogReadFailureKind.Malformed;
        }

        return PersistedOpsCatalogReadFailureKind.Unavailable;
    }
}
