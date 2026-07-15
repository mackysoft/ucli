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

        var firstDescriptorsResult = await ReadDescriptorsAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (!firstDescriptorsResult.IsSuccess)
        {
            return PersistedOpsCatalogReadResult.Failure(firstDescriptorsResult.ReadFailure!);
        }

        var firstReadResult = await ReadCatalogGenerationAsync(
                unityProject,
                firstDescriptorsResult.Snapshot!,
                firstDescriptorsResult.Freshness!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (firstReadResult.IsSuccess)
        {
            return firstReadResult;
        }

        var secondDescriptorsResult = await ReadDescriptorsAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (!secondDescriptorsResult.IsSuccess
            || secondDescriptorsResult.Snapshot!.IsSameGenerationAs(firstDescriptorsResult.Snapshot!))
        {
            return firstReadResult;
        }

        return await ReadCatalogGenerationAsync(
                unityProject,
                secondDescriptorsResult.Snapshot!,
                secondDescriptorsResult.Freshness!.Value,
                cancellationToken)
            .ConfigureAwait(false);
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

        var snapshot = opsCatalogResult.Value!;

        var freshnessResult = await freshnessEvaluator.ObserveAsync(
                unityProject,
                IndexFreshnessTarget.OpsCatalog,
                snapshot.SourceInputsHash,
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
            snapshot,
            freshnessResult.Freshness);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsDescribeReadResult> ReadDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        OpsCatalogDescriptorSnapshot catalogSnapshot,
        ValidatedOpsCatalogEntry catalogEntry,
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

        return PersistedOpsDescribeReadResult.Success(describeResult.Value!.Operation);
    }

    private async ValueTask<PersistedOpsCatalogReadResult> ReadCatalogGenerationAsync (
        ResolvedUnityProjectContext unityProject,
        OpsCatalogDescriptorSnapshot descriptors,
        IndexFreshness freshness,
        CancellationToken cancellationToken)
    {
        var operations = new List<ValidatedOpsOperation>(descriptors.Entries.Count);
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

        return PersistedOpsCatalogReadResult.Success(
            new OpsCatalogSnapshot(descriptors.GeneratedAtUtc, operations),
            freshness);
    }

    private static PersistedOpsCatalogReadFailure CreateArtifactFailure (IndexServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new PersistedOpsCatalogReadFailure(
            ClassifyArtifactFailure(error.Code),
            error.Code,
            error.Message);
    }

    private static PersistedOpsCatalogReadFailureKind ClassifyArtifactFailure (UcliCode errorCode)
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
