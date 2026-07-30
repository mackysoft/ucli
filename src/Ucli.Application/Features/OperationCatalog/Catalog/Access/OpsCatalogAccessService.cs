using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Implements ops catalog access flow across read-index and source fallback paths. </summary>
internal sealed class OpsCatalogAccessService : IOpsCatalogAccessService
{
    private readonly IPersistedOpsCatalogReader persistedOpsCatalogReader;

    private readonly IOpsCatalogSourceRefreshService sourceRefreshService;

    /// <summary> Initializes a new instance of the <see cref="OpsCatalogAccessService" /> class. </summary>
    /// <param name="persistedOpsCatalogReader"> The persisted ops-catalog reader dependency. </param>
    /// <param name="sourceRefreshService"> The source refresh dependency. </param>
    public OpsCatalogAccessService (
        IPersistedOpsCatalogReader persistedOpsCatalogReader,
        IOpsCatalogSourceRefreshService sourceRefreshService)
    {
        this.persistedOpsCatalogReader = persistedOpsCatalogReader ?? throw new ArgumentNullException(nameof(persistedOpsCatalogReader));
        this.sourceRefreshService = sourceRefreshService ?? throw new ArgumentNullException(nameof(sourceRefreshService));
    }

    /// <inheritdoc />
    public async ValueTask<OpsListReadResult> ReadListAsync (
        OpsPreflightContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        if (context.ReadIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadListFromSourceAsync(
                    context,
                    "readIndex disabled by mode.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedCatalogResult = await persistedOpsCatalogReader.ReadDescriptorsAsync(
                context.Context.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persistedCatalogResult.IsSuccess)
        {
            var failure = persistedCatalogResult.ReadFailure!;
            if (failure.Kind == PersistedOpsCatalogReadFailureKind.InvalidArgument)
            {
                return OpsListReadResult.Failure(
                    ApplicationFailure.InvalidInput(
                        failure.Message,
                        failure.ErrorCode,
                        instancePath: null,
                        startupFailure: null));
            }

            return await ReadListFromSourceAsync(
                    context,
                    failure.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedFreshness = persistedCatalogResult.Freshness!.Value;
        var persistedSnapshot = persistedCatalogResult.Snapshot!;
        if (context.ReadIndexMode == ReadIndexMode.AllowStale || persistedFreshness == IndexFreshness.Fresh)
        {
            return OpsListReadResult.Success(
                new OpsListReadOutput(
                    Snapshot: OpsCatalogListSnapshotFactory.FromDescriptors(persistedSnapshot),
                    AccessInfo: new OpsCatalogAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: OpsCatalogSource.Index,
                        Freshness: persistedFreshness,
                        GeneratedAtUtc: persistedSnapshot.GeneratedAtUtc,
                        FallbackReason: null)),
                "Read-index ops catalog hit.");
        }

        return await ReadListFromSourceAsync(
                context,
                        $"Existing ops index freshness is '{TextVocabulary.GetText(persistedFreshness)}'.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<OpsDescribeReadResult> ReadDescribeAsync (
        OpsPreflightContext context,
        string? operationName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(operationName))
        {
            return OpsDescribeReadResult.Failure(
                ApplicationFailure.InvalidInput(
                    "Operation name must not be empty.",
                    UcliCoreErrorCodes.InvalidArgument,
                    instancePath: null,
                    startupFailure: null));
        }

        if (context.ReadIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadDescribeFromSourceAsync(
                    context,
                    operationName,
                    "readIndex disabled by mode.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedCatalogResult = await persistedOpsCatalogReader.ReadDescriptorsAsync(
                context.Context.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persistedCatalogResult.IsSuccess)
        {
            var failure = persistedCatalogResult.ReadFailure!;
            if (failure.Kind == PersistedOpsCatalogReadFailureKind.InvalidArgument)
            {
                return OpsDescribeReadResult.Failure(
                    ApplicationFailure.InvalidInput(
                        failure.Message,
                        failure.ErrorCode,
                        instancePath: null,
                        startupFailure: null));
            }

            return await ReadDescribeFromSourceAsync(
                    context,
                    operationName,
                    failure.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedFreshness = persistedCatalogResult.Freshness!.Value;
        var persistedSnapshot = persistedCatalogResult.Snapshot!;
        if (context.ReadIndexMode != ReadIndexMode.AllowStale && persistedFreshness != IndexFreshness.Fresh)
        {
            return await ReadDescribeFromSourceAsync(
                    context,
                    operationName,
                    $"Existing ops index freshness is '{TextVocabulary.GetText(persistedFreshness)}'.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var catalogEntry = persistedSnapshot.Entries.FirstOrDefault(
            entry => string.Equals(entry.Name, operationName, StringComparison.Ordinal));
        if (catalogEntry == null)
        {
            return OpsDescribeReadResult.Failure(
                ApplicationFailure.InvalidInput(
                    $"Operation '{operationName}' is not available.",
                    UcliCoreErrorCodes.InvalidArgument,
                    instancePath: null,
                    startupFailure: null));
        }

        var describeResult = await persistedOpsCatalogReader.ReadDescribeAsync(
                context.Context.UnityProject,
                persistedSnapshot,
                catalogEntry,
                cancellationToken)
            .ConfigureAwait(false);
        if (!describeResult.IsSuccess)
        {
            return await ReadDescribeFromSourceAsync(
                    context,
                    operationName,
                    describeResult.ReadFailure!.Message,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return OpsDescribeReadResult.Success(
            new OpsDescribeReadOutput(
                Operation: describeResult.Operation!,
                AccessInfo: new OpsCatalogAccessInfo(
                    Used: true,
                    Hit: true,
                    Source: OpsCatalogSource.Index,
                    Freshness: persistedFreshness,
                    GeneratedAtUtc: persistedSnapshot.GeneratedAtUtc,
                    FallbackReason: null)),
            $"Read-index ops describe hit for '{operationName}'.");
    }

    private async ValueTask<OpsListReadResult> ReadListFromSourceAsync (
        OpsPreflightContext context,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var refreshResult = await sourceRefreshService.RefreshAsync(
                context.Context.UnityProject,
                context.Context.Config,
                context.Mode,
                context.Timeout,
                context.FailFast,
                fallbackReason,
                cancellationToken)
            .ConfigureAwait(false);
        if (refreshResult is OpsCatalogSourceRefreshResult.Failed failedRefresh)
        {
            return OpsListReadResult.Failure(failedRefresh.Error);
        }

        var successfulRefresh = refreshResult as OpsCatalogSourceRefreshResult.Succeeded
            ?? throw new InvalidOperationException($"Unsupported ops-catalog refresh result '{refreshResult.GetType().Name}'.");
        return OpsListReadResult.Success(
            new OpsListReadOutput(
                Snapshot: OpsCatalogListSnapshotFactory.FromCatalog(successfulRefresh.Snapshot),
                AccessInfo: new OpsCatalogAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: OpsCatalogSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: successfulRefresh.Snapshot.GeneratedAtUtc,
                    FallbackReason: successfulRefresh.FallbackReason)),
            "Ops catalog read completed.");
    }

    private async ValueTask<OpsDescribeReadResult> ReadDescribeFromSourceAsync (
        OpsPreflightContext context,
        string operationName,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var refreshResult = await sourceRefreshService.RefreshAsync(
                context.Context.UnityProject,
                context.Context.Config,
                context.Mode,
                context.Timeout,
                context.FailFast,
                fallbackReason,
                cancellationToken)
            .ConfigureAwait(false);
        if (refreshResult is OpsCatalogSourceRefreshResult.Failed failedRefresh)
        {
            return OpsDescribeReadResult.Failure(failedRefresh.Error);
        }

        var successfulRefresh = refreshResult as OpsCatalogSourceRefreshResult.Succeeded
            ?? throw new InvalidOperationException($"Unsupported ops-catalog refresh result '{refreshResult.GetType().Name}'.");
        var operation = successfulRefresh.Snapshot.Operations.FirstOrDefault(
            operation => string.Equals(operation.Name, operationName, StringComparison.Ordinal));
        if (operation == null)
        {
            return OpsDescribeReadResult.Failure(
                ApplicationFailure.InvalidInput(
                    $"Operation '{operationName}' is not available.",
                    UcliCoreErrorCodes.InvalidArgument,
                    instancePath: null,
                    startupFailure: null));
        }

        return OpsDescribeReadResult.Success(
            new OpsDescribeReadOutput(
                Operation: operation,
                AccessInfo: new OpsCatalogAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: OpsCatalogSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: successfulRefresh.Snapshot.GeneratedAtUtc,
                    FallbackReason: successfulRefresh.FallbackReason)),
            $"Ops describe completed for '{operationName}'.");
    }
}
