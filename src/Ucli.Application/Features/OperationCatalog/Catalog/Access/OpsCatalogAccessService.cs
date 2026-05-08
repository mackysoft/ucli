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
    public async ValueTask<OpsCatalogReadResult> Read (
        OpsPreflightContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        if (context.ReadIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadCatalogFromSource(
                    context,
                    "readIndex disabled by mode.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedCatalogResult = await persistedOpsCatalogReader.Read(
                context.Context.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persistedCatalogResult.IsSuccess)
        {
            var failure = persistedCatalogResult.ReadFailure!;
            if (failure.Kind == PersistedOpsCatalogReadFailureKind.InvalidArgument)
            {
                return OpsCatalogReadResult.Failure(
                    failure.Message,
                    failure.ErrorCode);
            }

            return await ReadCatalogFromSource(
                    context,
                    failure.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var persistedFreshness = persistedCatalogResult.Freshness!.Value;
        var persistedSnapshot = persistedCatalogResult.Snapshot!;
        if (context.ReadIndexMode == ReadIndexMode.AllowStale || persistedFreshness == IndexFreshness.Fresh)
        {
            return OpsCatalogReadResult.Success(
                new OpsCatalogReadOutput(
                    Snapshot: persistedSnapshot,
                    AccessInfo: new OpsCatalogAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: OpsCatalogSource.Index,
                        Freshness: persistedFreshness,
                        GeneratedAtUtc: persistedSnapshot.GeneratedAtUtc,
                        FallbackReason: null)),
                "Read-index ops catalog hit.");
        }

        return await ReadCatalogFromSource(
                context,
                $"Existing ops index freshness is '{ReadIndexAccessUtilities.DescribeFreshness(persistedFreshness)}'.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<OpsCatalogReadResult> ReadCatalogFromSource (
        OpsPreflightContext context,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var refreshResult = await sourceRefreshService.Refresh(
                context.Context.UnityProject,
                context.Context.Config,
                context.Mode,
                context.Timeout,
                context.FailFast,
                fallbackReason,
                cancellationToken)
            .ConfigureAwait(false);
        if (!refreshResult.IsSuccess)
        {
            return OpsCatalogReadResult.Failure(
                refreshResult.Message,
                refreshResult.ErrorCode!.Value);
        }

        return OpsCatalogReadResult.Success(
            new OpsCatalogReadOutput(
                Snapshot: refreshResult.Snapshot!,
                AccessInfo: new OpsCatalogAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: OpsCatalogSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: refreshResult.Snapshot!.GeneratedAtUtc,
                    FallbackReason: refreshResult.FallbackReason)),
            "Ops catalog read completed.");
    }

}
