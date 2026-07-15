using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;
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

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await SearchFromSourceAsync(
                    project,
                    config,
                    mode,
                    timeout,
                    query,
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
                    query,
                    lookupResult.Error.Message,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupSnapshot = lookupResult.Value!;
        var readPostconditionEvaluation = await MutationReadPostconditionAccessEvaluator.EvaluateAssetSearchAsync(
                mutationReadPostconditionStore,
                project,
                lookupSnapshot.GeneratedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readPostconditionEvaluation.CanUseIndex)
        {
            return await SearchFromSourceAsync(
                    project,
                    config,
                    mode,
                    timeout,
                    query,
                    readPostconditionEvaluation.FallbackReason!,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await freshnessEvaluator.ObserveAsync(
                project,
                IndexFreshnessTarget.AssetSearchLookup,
                lookupSnapshot.SourceInputsHash,
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
                    FilterEntries(lookupSnapshot.Entries, query),
                    new AssetLookupAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: AssetLookupSource.Index,
                        Freshness: freshnessResult.Freshness,
                        GeneratedAtUtc: lookupSnapshot.GeneratedAtUtc,
                        FallbackReason: null)),
                "Asset-search lookup read completed.");
        }

        return await SearchFromSourceAsync(
                project,
                config,
                mode,
                timeout,
                query,
                $"Existing asset-search index freshness is '{ContractLiteralCodec.ToValue(freshnessResult.Freshness)}'.",
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<AssetSearchLookupReadResult> SearchFromSourceAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        AssetSearchLookupQuery query,
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
            return AssetSearchLookupReadResult.Failure(refreshResult.Message, refreshResult.ErrorCode!);
        }

        var snapshot = refreshResult.Snapshot!;

        return AssetSearchLookupReadResult.Success(
            new AssetSearchLookupReadOutput(
                FilterEntries(snapshot.AssetSearchEntries, query),
                new AssetLookupAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: AssetLookupSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: snapshot.GeneratedAtUtc,
                    FallbackReason: refreshResult.FallbackReason)),
            "Asset-search lookup read completed.");
    }

    private static IReadOnlyList<AssetSearchLookupEntry> FilterEntries (
        IReadOnlyList<AssetSearchLookupEntry> entries,
        AssetSearchLookupQuery query)
    {
        var matches = new List<AssetSearchLookupEntry>(entries.Count);
        var pathPrefix = query.PathPrefix;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (query.TypeId != null
                && !entry.ContainsSearchTypeId(query.TypeId))
            {
                continue;
            }

            if (pathPrefix != null
                && !UnityAssetPathContract.IsSameOrDescendantAssetPath(pathPrefix.Value, entry.AssetPath.Value))
            {
                continue;
            }

            if (query.NameContains != null
                && entry.Name.IndexOf(query.NameContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            matches.Add(entry);
        }

        return matches;
    }
}
