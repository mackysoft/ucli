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

        var descriptorsResult = await ReadDescriptorsAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (!descriptorsResult.IsSuccess)
        {
            return PersistedOpsCatalogReadResult.Failure(descriptorsResult.ReadFailure!);
        }

        var descriptors = descriptorsResult.Snapshot!;
        var operations = new List<IndexOpEntryJsonContract>(descriptors.Entries.Count);
        for (var i = 0; i < descriptors.Entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var describeResult = await ReadDescribeAsync(
                    unityProject,
                    descriptors,
                    descriptors.Entries[i],
                    cancellationToken)
                .ConfigureAwait(false);
            if (!describeResult.IsSuccess)
            {
                return PersistedOpsCatalogReadResult.Failure(describeResult.ReadFailure!);
            }

            operations.Add(describeResult.Operation!);
        }

        if (!OpsCatalogSnapshot.TryCreate(
                descriptors.GeneratedAtUtc,
                operations,
                "operations",
                out var snapshot,
                out var validationError))
        {
            return PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Malformed,
                    ReadIndexErrorCodes.ReadIndexFormatInvalid,
                    $"Index contract file 'ops.describe/<opKey>.json' is malformed. {validationError}"));
        }

        return PersistedOpsCatalogReadResult.Success(snapshot!, descriptorsResult.Freshness!.Value);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsCatalogDescriptorReadResult> ReadDescriptorsAsync (
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
            return PersistedOpsCatalogDescriptorReadResult.Failure(CreateArtifactFailure(opsCatalogResult.Error!));
        }

        var opsCatalog = opsCatalogResult.Value!;
        if (!OpsCatalogDescriptorSnapshot.TryCreate(
                opsCatalog.GeneratedAtUtc,
                opsCatalog.SourceInputsHash,
                opsCatalog.Entries,
                "entries",
                out var snapshot,
                out var validationError))
        {
            return PersistedOpsCatalogDescriptorReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Malformed,
                    ReadIndexErrorCodes.ReadIndexFormatInvalid,
                    $"Index contract file 'ops.catalog.json' is malformed. {validationError}"));
        }

        var freshnessResult = await freshnessEvaluator.ObserveAsync(
                unityProject,
                IndexFreshnessTarget.OpsCatalog,
                snapshot!.SourceInputsHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return PersistedOpsCatalogDescriptorReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.FreshnessUnavailable,
                    freshnessResult.Error!.Code,
                    freshnessResult.Error.Message));
        }

        return PersistedOpsCatalogDescriptorReadResult.Success(
            snapshot!,
            freshnessResult.Freshness);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsDescribeReadResult> ReadDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        OpsCatalogDescriptorSnapshot catalogSnapshot,
        IndexOpsCatalogEntryJsonContract catalogEntry,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(catalogSnapshot);
        ArgumentNullException.ThrowIfNull(catalogEntry);

        var describeResult = await artifactReader.ReadOpsDescribeAsync(
                unityProject,
                catalogEntry,
                catalogSnapshot.SourceInputsHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (!describeResult.IsSuccess)
        {
            return PersistedOpsDescribeReadResult.Failure(CreateArtifactFailure(describeResult.Error!));
        }

        return PersistedOpsDescribeReadResult.Success(describeResult.Value!.Operation!);
    }

    private static PersistedOpsCatalogReadFailure CreateArtifactFailure (IndexServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new PersistedOpsCatalogReadFailure(
            ClassifyArtifactFailure(error.Code),
            error.Code,
            error.Message);
    }

    private static PersistedOpsCatalogReadFailureKind ClassifyArtifactFailure (UcliCodeValue errorCode)
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
