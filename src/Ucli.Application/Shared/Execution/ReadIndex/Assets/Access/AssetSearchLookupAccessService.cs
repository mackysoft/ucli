using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Configuration;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Implements asset-search lookup access flow across read-index and source fallback paths. </summary>
internal sealed class AssetSearchLookupAccessService : IAssetSearchLookupAccessService
{
    private readonly IReadIndexArtifactReader artifactReader;
    private readonly IReadIndexFreshnessEvaluator freshnessEvaluator;
    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;
    private readonly IAssetLookupSourceRefreshService assetLookupSourceRefreshService;

    /// <summary> Initializes a new instance of the <see cref="AssetSearchLookupAccessService" /> class. </summary>
    public AssetSearchLookupAccessService (
        IReadIndexArtifactReader artifactReader,
        IReadIndexFreshnessEvaluator freshnessEvaluator,
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        IAssetLookupSourceRefreshService assetLookupSourceRefreshService)
    {
        this.artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        this.freshnessEvaluator = freshnessEvaluator ?? throw new ArgumentNullException(nameof(freshnessEvaluator));
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
        this.assetLookupSourceRefreshService = assetLookupSourceRefreshService ?? throw new ArgumentNullException(nameof(assetLookupSourceRefreshService));
    }

    /// <inheritdoc />
    public async ValueTask<AssetSearchLookupReadResult> SearchAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        AssetSearchLookupQuery query,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (!AssetLookupAccessUtilities.TryNormalizeSearchQuery(query, out var normalizedQuery, out var errorMessage))
        {
            return AssetSearchLookupReadResult.Failure(errorMessage, UcliCoreErrorCodes.InvalidArgument);
        }

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await SearchFromSourceAsync(
                    project,
                    config,
                    mode,
                    timeout,
                    normalizedQuery,
                    "readIndex disabled by mode.",
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupResult = await artifactReader.ReadAssetSearchLookupAsync(
                project,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lookupResult.IsSuccess)
        {
            if (lookupResult.Error!.Code == UcliCoreErrorCodes.InvalidArgument)
            {
                return AssetSearchLookupReadResult.Failure(
                    lookupResult.Error.Message,
                    lookupResult.Error.Code);
            }

            return await SearchFromSourceAsync(
                    project,
                    config,
                    mode,
                    timeout,
                    normalizedQuery,
                    lookupResult.Error.Message,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var readPostconditionEvaluation = await MutationReadPostconditionAccessEvaluator.EvaluateAssetSearchAsync(
                mutationReadPostconditionStore,
                project,
                lookupResult.Value!.GeneratedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readPostconditionEvaluation.CanUseIndex)
        {
            return await SearchFromSourceAsync(
                    project,
                    config,
                    mode,
                    timeout,
                    normalizedQuery,
                    readPostconditionEvaluation.FallbackReason!,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await freshnessEvaluator.ObserveAsync(
                project,
                IndexFreshnessTarget.AssetSearchLookup,
                lookupResult.Value!.SourceInputsHash,
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

        return await SearchFromSourceAsync(
                project,
                config,
                mode,
                timeout,
                normalizedQuery,
                $"Existing asset-search index freshness is '{ReadIndexAccessUtilities.DescribeFreshness(freshnessResult.Freshness)}'.",
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<AssetSearchLookupReadResult> SearchFromSourceAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        AssetSearchLookupQuery normalizedQuery,
        string fallbackReason,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var refreshResult = await assetLookupSourceRefreshService.RefreshAsync(
                project,
                config,
                UcliCommandIds.Query,
                mode,
                timeout,
                fallbackReason,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        if (!refreshResult.IsSuccess)
        {
            return AssetSearchLookupReadResult.Failure(refreshResult.Message, refreshResult.ErrorCode!.Value);
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
