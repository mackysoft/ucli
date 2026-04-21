using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;

/// <summary> Implements asset-search lookup access flow across read-index and source fallback paths. </summary>
internal sealed class AssetSearchLookupAccessService : IAssetSearchLookupAccessService
{
    private readonly IIndexCatalogReader indexCatalogReader;
    private readonly IIndexFreshnessEvaluator indexFreshnessEvaluator;
    private readonly IAssetLookupSourceRefreshService assetLookupSourceRefreshService;

    /// <summary> Initializes a new instance of the <see cref="AssetSearchLookupAccessService" /> class. </summary>
    public AssetSearchLookupAccessService (
        IIndexCatalogReader indexCatalogReader,
        IIndexFreshnessEvaluator indexFreshnessEvaluator,
        IAssetLookupSourceRefreshService assetLookupSourceRefreshService)
    {
        this.indexCatalogReader = indexCatalogReader ?? throw new ArgumentNullException(nameof(indexCatalogReader));
        this.indexFreshnessEvaluator = indexFreshnessEvaluator ?? throw new ArgumentNullException(nameof(indexFreshnessEvaluator));
        this.assetLookupSourceRefreshService = assetLookupSourceRefreshService ?? throw new ArgumentNullException(nameof(assetLookupSourceRefreshService));
    }

    /// <inheritdoc />
    public async ValueTask<AssetSearchLookupReadResult> Search (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        AssetSearchLookupQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (!AssetLookupAccessUtilities.TryNormalizeSearchQuery(query, out var normalizedQuery, out var errorMessage))
        {
            return AssetSearchLookupReadResult.Failure(errorMessage, IpcErrorCodes.InvalidArgument);
        }

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await SearchFromSource(
                    project,
                    config,
                    mode,
                    timeout,
                    readIndexMode,
                    normalizedQuery,
                    "readIndex disabled by mode.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupResult = await indexCatalogReader.ReadAssetSearchLookup(
                project.RepositoryRoot,
                project.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lookupResult.IsSuccess)
        {
            if (string.Equals(lookupResult.Error!.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            {
                return AssetSearchLookupReadResult.Failure(
                    lookupResult.Error.Message,
                    lookupResult.Error.Code);
            }

            return await SearchFromSource(
                    project,
                    config,
                    mode,
                    timeout,
                    readIndexMode,
                    normalizedQuery,
                    lookupResult.Error.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await indexFreshnessEvaluator.Evaluate(
                project.UnityProjectRoot,
                IndexFreshnessTarget.AssetSearchLookup,
                lookupResult.Value!.SourceInputsHash,
                ReadIndexMode.AllowStale,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return AssetSearchLookupReadResult.Failure(
                freshnessResult.Error!.Message,
                freshnessResult.Error.Code);
        }

        if (readIndexMode == ReadIndexMode.AllowStale || freshnessResult.Freshness == IndexFreshness.Fresh)
        {
            return AssetSearchLookupReadResult.Success(
                new AssetSearchLookupReadOutput(
                    FilterEntries(lookupResult.Value!.Entries!, normalizedQuery),
                    new AssetLookupAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: AssetLookupSource.Index,
                        Freshness: freshnessResult.Freshness,
                        GeneratedAtUtc: lookupResult.Value.GeneratedAtUtc,
                        FallbackReason: null)),
                "Asset-search lookup read completed.");
        }

        return await SearchFromSource(
                project,
                config,
                mode,
                timeout,
                readIndexMode,
                normalizedQuery,
                $"Existing asset-search index freshness is '{AssetLookupAccessUtilities.DescribeFreshness(freshnessResult.Freshness)}'.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<AssetSearchLookupReadResult> SearchFromSource (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        AssetSearchLookupQuery normalizedQuery,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var refreshResult = await assetLookupSourceRefreshService.Refresh(
                project,
                config,
                UcliCommandIds.Query,
                mode,
                timeout,
                readIndexMode,
                fallbackReason,
                cancellationToken)
            .ConfigureAwait(false);
        if (!refreshResult.IsSuccess)
        {
            return AssetSearchLookupReadResult.Failure(refreshResult.Message, refreshResult.ErrorCode!);
        }

        var response = refreshResult.Response!;
        return AssetSearchLookupReadResult.Success(
            new AssetSearchLookupReadOutput(
                FilterEntries(response.AssetSearchEntries!, normalizedQuery),
                new AssetLookupAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: AssetLookupSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: response.GeneratedAtUtc,
                    FallbackReason: refreshResult.FallbackReason)),
            "Asset-search lookup read completed.");
    }

    private static IReadOnlyList<IndexAssetSearchEntryJsonContract> FilterEntries (
        IReadOnlyList<IndexAssetSearchEntryJsonContract> entries,
        AssetSearchLookupQuery query)
    {
        var matches = new List<IndexAssetSearchEntryJsonContract>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (query.TypeId != null
                && !ContainsTypeId(entry.SearchTypeIds!, query.TypeId))
            {
                continue;
            }

            if (query.PathPrefix != null
                && !entry.AssetPath!.StartsWith(query.PathPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (query.NameContains != null
                && entry.Name!.IndexOf(query.NameContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            matches.Add(entry);
        }

        return matches;
    }

    private static bool ContainsTypeId (
        IReadOnlyList<string> searchTypeIds,
        string typeId)
    {
        for (var i = 0; i < searchTypeIds.Count; i++)
        {
            if (string.Equals(searchTypeIds[i], typeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}