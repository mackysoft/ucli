using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Implements GUID-path lookup access flow across read-index and source fallback paths. </summary>
internal sealed class GuidPathLookupAccessService : IGuidPathLookupAccessService
{
    private readonly IReadIndexArtifactReader artifactReader;
    private readonly IReadIndexFreshnessEvaluator freshnessEvaluator;
    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;
    private readonly IAssetLookupSourceRefreshService assetLookupSourceRefreshService;

    /// <summary> Initializes a new instance of the <see cref="GuidPathLookupAccessService" /> class. </summary>
    public GuidPathLookupAccessService (
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
    public ValueTask<GuidPathLookupReadResult> TryResolveAssetGuid (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string assetGuid,
        CancellationToken cancellationToken = default)
    {
        if (!AssetLookupAccessUtilities.TryNormalizeAssetGuid(assetGuid, out var normalizedAssetGuid, out var errorMessage))
        {
            return ValueTask.FromResult(GuidPathLookupReadResult.Failure(errorMessage, IpcErrorCodes.InvalidArgument));
        }

        return ReadCore(
            project,
            config,
            mode,
            timeout,
            readIndexMode,
            fallbackReason: null,
            static (entries, key) => FindByAssetGuid(entries, key),
            normalizedAssetGuid,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GuidPathLookupReadResult> TryResolveAssetPath (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string assetPath,
        CancellationToken cancellationToken = default)
    {
        if (!AssetLookupAccessUtilities.TryNormalizeAssetPath(assetPath, out var normalizedAssetPath, out var errorMessage))
        {
            return ValueTask.FromResult(GuidPathLookupReadResult.Failure(errorMessage, IpcErrorCodes.InvalidArgument));
        }

        return ReadCore(
            project,
            config,
            mode,
            timeout,
            readIndexMode,
            fallbackReason: null,
            static (entries, key) => FindByAssetPath(entries, key),
            normalizedAssetPath,
            cancellationToken);
    }

    private async ValueTask<GuidPathLookupReadResult> ReadCore (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string? fallbackReason,
        Func<IReadOnlyList<IndexGuidPathEntryJsonContract>, string, IndexGuidPathEntryJsonContract?> resolver,
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(resolver);
        cancellationToken.ThrowIfCancellationRequested();

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadFromSource(
                    project,
                    config,
                    mode,
                    timeout,
                    readIndexMode,
                    "readIndex disabled by mode.",
                    resolver,
                    key,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupResult = await artifactReader.ReadGuidPathLookup(
                project.RepositoryRoot,
                project.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lookupResult.IsSuccess)
        {
            if (string.Equals(lookupResult.Error!.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            {
                return GuidPathLookupReadResult.Failure(
                    lookupResult.Error.Message,
                    lookupResult.Error.Code);
            }

            return await ReadFromSource(
                    project,
                    config,
                    mode,
                    timeout,
                    readIndexMode,
                    lookupResult.Error.Message,
                    resolver,
                    key,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var readPostconditionEvaluation = await MutationReadPostconditionAccessEvaluator.EvaluateGuidPath(
                mutationReadPostconditionStore,
                project,
                lookupResult.Value!.GeneratedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readPostconditionEvaluation.CanUseIndex)
        {
            return await ReadFromSource(
                    project,
                    config,
                    mode,
                    timeout,
                    readIndexMode,
                    readPostconditionEvaluation.FallbackReason!,
                    resolver,
                    key,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await freshnessEvaluator.Evaluate(
                project.UnityProjectRoot,
                IndexFreshnessTarget.GuidPathLookup,
                lookupResult.Value!.SourceInputsHash,
                ReadIndexMode.AllowStale,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return GuidPathLookupReadResult.Failure(
                freshnessResult.Error!.Message,
                freshnessResult.Error.Code);
        }

        if (readIndexMode == ReadIndexMode.AllowStale || freshnessResult.Freshness == IndexFreshness.Fresh)
        {
            return GuidPathLookupReadResult.Success(
                new GuidPathLookupReadOutput(
                    resolver(lookupResult.Value!.Entries!, key),
                    new AssetLookupAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: AssetLookupSource.Index,
                        Freshness: freshnessResult.Freshness,
                        GeneratedAtUtc: lookupResult.Value.GeneratedAtUtc,
                        FallbackReason: null)),
                "GUID-path lookup read completed.");
        }

        fallbackReason = $"Existing guid-path index freshness is '{AssetLookupAccessUtilities.DescribeFreshness(freshnessResult.Freshness)}'.";
        return await ReadFromSource(
                project,
                config,
                mode,
                timeout,
                readIndexMode,
                fallbackReason,
                resolver,
                key,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<GuidPathLookupReadResult> ReadFromSource (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string fallbackReason,
        Func<IReadOnlyList<IndexGuidPathEntryJsonContract>, string, IndexGuidPathEntryJsonContract?> resolver,
        string key,
        CancellationToken cancellationToken)
    {
        var refreshResult = await assetLookupSourceRefreshService.Refresh(
                project,
                config,
                UcliCommandIds.Resolve,
                mode,
                timeout,
                readIndexMode,
                fallbackReason,
                failFast: false,
                cancellationToken)
            .ConfigureAwait(false);
        if (!refreshResult.IsSuccess)
        {
            return GuidPathLookupReadResult.Failure(refreshResult.Message, refreshResult.ErrorCode!);
        }

        var response = refreshResult.Response!;
        return GuidPathLookupReadResult.Success(
            new GuidPathLookupReadOutput(
                resolver(response.GuidPathEntries!, key),
                new AssetLookupAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: AssetLookupSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: response.GeneratedAtUtc,
                    FallbackReason: refreshResult.FallbackReason)),
            "GUID-path lookup read completed.");
    }

    private static IndexGuidPathEntryJsonContract? FindByAssetGuid (
        IReadOnlyList<IndexGuidPathEntryJsonContract> entries,
        string assetGuid)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].AssetGuid, assetGuid, StringComparison.Ordinal))
            {
                return entries[i];
            }
        }

        return null;
    }

    private static IndexGuidPathEntryJsonContract? FindByAssetPath (
        IReadOnlyList<IndexGuidPathEntryJsonContract> entries,
        string assetPath)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].AssetPath, assetPath, StringComparison.Ordinal))
            {
                return entries[i];
            }
        }

        return null;
    }
}
